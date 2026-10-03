using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GamePhone.Shop
{
    /// <summary>
    /// После входа в магазин покупатель обходит несколько случайных полок из
    /// _shelfPoints (у каждой - свой товар, см. ShopShelfStock), поворачивается
    /// к полке и "берёт" именно тот товар, что на ней реально лежит - модель
    /// с полки просто исчезает (ShopShelfStock.TryTake), никакого визуального
    /// товара в руках у покупателя не появляется.
    ///
    /// Пока идёт от полки к полке, физика (Rigidbody) остаётся включённой всё
    /// время - как и в очереди, покупателя можно толкнуть, на него действует
    /// гравитация и т.д. NavMeshAgent используется только как "мозг": считает
    /// путь и обходит препятствия (полки, стены, других покупателей), а
    /// реально двигает Rigidbody через linearVelocity (agent.updatePosition/
    /// updateRotation выключены - агент сам transform не трогает). Если
    /// NavMeshAgent на префабе не найден - используется прямолинейное
    /// движение (без обхода препятствий), тоже через Rigidbody.
    ///
    /// Собранный список товаров передаётся в CustomerQueueController.AddCustomer -
    /// именно ЭТИ товары потом появятся на кассе, когда покупатель дойдёт до
    /// начала очереди (см. CustomerQueueController.OnCustomerArrivedAtRegister
    /// и CheckoutManager) - что взял с полки, то и сканируется.
    /// </summary>
    public class CustomerShopper : MonoBehaviour
    {
        [Tooltip("Полки - куда покупатель подходит, чтобы \"взять\" товар, который на ней реально лежит")]
        [SerializeField] private ShopShelfStock[] _shelfPoints;
        [SerializeField] private int _minShelves = 2;
        [SerializeField] private int _maxShelves = 4;
        [SerializeField] private float _walkSpeed = 2f;
        [SerializeField] private float _rotationSpeed = 540f;
        [Tooltip("Насколько близко к полке считается \"дошёл\"")]
        [SerializeField] private float _stopDistance = 0.15f;
        [Tooltip("Сколько секунд стоит у полки, прежде чем идти дальше")]
        [SerializeField] private float _pickupPause = 0.5f;
        [Tooltip("Имя bool-параметра в Animator, включающего анимацию ходьбы")]
        [SerializeField] private string _isWalkingParam = "isWalking";

        [Header("Куда покупатель передаётся после похода по полкам")]
        [SerializeField] private CustomerQueueController _queue;

        [Header("Воровство")]
        [Tooltip("Шанс (0-1), что покупатель решит уйти к выходу без оплаты, вместо очереди на кассу - " +
                 "но только если сейчас вообще разрешено ещё одному вору появиться, см. TheftDifficultyManager")]
        [SerializeField] private float _theftChance = 0.15f;

        // Возвращает саму корутину - нужна CustomerPopulationTracker (см. Initialize),
        // чтобы в конце смены можно было оборвать поход именно ЭТОГО покупателя
        // (StartShopping общий на всех сразу, каждый идёт своей корутиной параллельно)
        public Coroutine StartShopping(Transform customer, Rigidbody rb, Animator animator)
        {
            return StartCoroutine(ShoppingRoutine(customer, rb, animator));
        }

        private IEnumerator ShoppingRoutine(Transform customer, Rigidbody rb, Animator animator)
        {
            var order = new List<ShopItemData>();
            PushStagger stagger = customer.GetComponentInChildren<PushStagger>();

            bool canShop = _shelfPoints != null && _shelfPoints.Length > 0;

            // Настраиваем агента независимо от того, есть ли вообще полки - он
            // ещё понадобится ниже, чтобы дойти до кассы в обход препятствий,
            // даже если по полкам покупатель вообще не ходил.
            NavMeshAgent agent = customer.GetComponentInChildren<NavMeshAgent>();

            if (agent != null)
            {
                // Пока покупатель поднимался из-под пола, агент мог быть включён
                // вне запечённого NavMesh и "потеряться" - Warp принудительно
                // ставит его на ближайшую точку NavMesh прямо сейчас, когда
                // покупатель уже реально стоит в магазине.
                agent.enabled = true;
                agent.Warp(customer.position);

                if (agent.isOnNavMesh)
                {
                    ConfigureAgent(agent);
                    // Сам transform агент не двигает - только считает путь/обход
                    // препятствий, а реально двигает Rigidbody (см. WalkTo).
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                }
                else
                {
                    // Рядом вообще нет запечённого NavMesh - идём по старинке,
                    // по прямой, без обхода препятствий.
                    agent.enabled = false;
                    agent = null;
                }
            }

            if (canShop)
            {
                int shelfCount = Random.Range(_minShelves, _maxShelves + 1);
                List<ShopShelfStock> shelvesToVisit = PickRandomShelves(shelfCount);

                foreach (ShopShelfStock shelf in shelvesToVisit)
                {
                    if (shelf == null) continue;

                    yield return StartCoroutine(WalkTo(customer, rb, animator, stagger, agent, shelf.transform.position));

                    if (customer == null) yield break;

                    yield return StartCoroutine(FaceTarget(customer, rb, shelf.transform.position));

                    if (customer == null) yield break;

                    // Товар мог за это время забрать другой покупатель - тогда просто
                    // идём дальше без остановки на этой полке
                    if (shelf.TryTake())
                        order.Add(shelf.Data);

                    if (_pickupPause > 0f)
                        yield return new WaitForSeconds(_pickupPause);
                }
            }

            if (customer == null) yield break;

            // Если что-то реально взял - есть шанс, что уйдёт без оплаты вместо
            // очереди. Заметить и поймать такого - см. ItemDragController.TryGrabThief/CustomerThief.
            // Дополнительно спрашиваем TheftDifficultyManager - сколько воров сейчас
            // вообще разрешено одновременно (растёт по ходу смены: 1, 2, 4, 8...) -
            // если лимит уже занят, покупатель просто идёт в очередь как честный,
            // сколько бы ни повезло с Random.value.
            bool theftAllowedNow = TheftDifficultyManager.Instance == null || TheftDifficultyManager.Instance.CanBecomeThief();
            bool isThief = order.Count > 0 && Random.value < _theftChance && theftAllowedNow;

            if (isThief && _queue != null)
            {
                // Дальше им занимается CustomerThief/CustomerQueueController, а не
                // эта корутина - см. CustomerPopulationTracker.ForceLeave
                customer.GetComponent<CustomerPopulationTracker>()?.MarkLeftShoppingStage();

                TheftDifficultyManager.Instance?.RegisterThiefStarted();

                // Агент дальше не нужен здесь - до выхода поведёт свой собственный
                // (см. CustomerQueueController.WalkToExitPoint), он сам его заново найдёт и включит.
                if (agent != null)
                {
                    agent.ResetPath();
                    agent.enabled = false;
                }

                CustomerThief thief = customer.gameObject.AddComponent<CustomerThief>();
                thief.MarkAsThief(order, _queue, animator, rb, stagger);
                _queue.SendThiefToExit(customer, animator, rb, stagger);
            }
            else
            {
                // Агент дальше не нужен здесь - CustomerQueueController.AddCustomer
                // сам заново найдёт и включит его, чтобы довести покупателя прямо
                // от текущего места (пусть даже у дальней полки) до его реального
                // места в очереди по NavMesh - см. CustomerQueueController.WalkInToSlot.
                if (agent != null)
                {
                    agent.ResetPath();
                    agent.enabled = false;
                }

                if (customer == null) yield break;

                // Дальше им занимается CustomerQueueController, а не эта корутина -
                // см. CustomerPopulationTracker.ForceLeave
                customer.GetComponent<CustomerPopulationTracker>()?.MarkLeftShoppingStage();

                _queue?.AddCustomer(customer, order);
            }
        }

        // Вызывается CustomerPopulationTracker.ForceLeave в конце смены - обрывает
        // поход по полкам (уже сделано в ForceLeave через StopCoroutine) и
        // отправляет покупателя прямо к выходу, минуя и полки, и очередь.
        // Переиспользует WalkTo - тот же способ ходьбы, что и по полкам.
        public void SendToExit(Transform customer, Transform exitPoint)
        {
            if (customer == null || exitPoint == null) return;

            Rigidbody rb = customer.GetComponentInChildren<Rigidbody>();
            Animator animator = customer.GetComponentInChildren<Animator>();
            PushStagger stagger = customer.GetComponentInChildren<PushStagger>();

            StartCoroutine(LeaveRoutine(customer, rb, animator, stagger, exitPoint));
        }

        private IEnumerator LeaveRoutine(Transform customer, Rigidbody rb, Animator animator, PushStagger stagger, Transform exitPoint)
        {
            NavMeshAgent agent = customer.GetComponentInChildren<NavMeshAgent>();

            if (agent != null)
            {
                // Мог остаться выключенным/потерянным с прошлого использования
                // (см. ShoppingRoutine) - Warp принудительно ставит на ближайшую
                // точку NavMesh прямо сейчас.
                agent.enabled = true;
                agent.Warp(customer.position);

                if (agent.isOnNavMesh)
                {
                    ConfigureAgent(agent);
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                }
                else
                {
                    agent.enabled = false;
                    agent = null;
                }
            }

            yield return StartCoroutine(WalkTo(customer, rb, animator, stagger, agent, exitPoint.position));

            if (agent != null)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            if (customer != null)
                Destroy(customer.gameObject);
        }

        private void ConfigureAgent(NavMeshAgent agent)
        {
            if (agent == null) return;

            agent.speed = _walkSpeed;
            agent.angularSpeed = _rotationSpeed;
            agent.stoppingDistance = _stopDistance;
        }

        // Без повторов - каждая полка в списке посещается максимум один раз.
        // Берём только те полки, на которых сейчас реально есть товар.
        private List<ShopShelfStock> PickRandomShelves(int count)
        {
            var pool = new List<ShopShelfStock>();
            foreach (ShopShelfStock shelf in _shelfPoints)
                if (shelf != null && shelf.HasStock)
                    pool.Add(shelf);

            var result = new List<ShopShelfStock>();

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        // Плавно доворачивает покупателя лицом к полке, прежде чем он "возьмёт" товар
        private IEnumerator FaceTarget(Transform customer, Rigidbody rb, Vector3 target)
        {
            Vector3 toTarget = target - customer.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) yield break;

            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            while (customer != null && Quaternion.Angle(customer.rotation, targetRot) > 2f)
            {
                Quaternion newRot = Quaternion.RotateTowards(customer.rotation, targetRot, _rotationSpeed * Time.fixedDeltaTime);

                if (rb != null)
                    rb.MoveRotation(newRot);
                else
                    customer.rotation = newRot;

                yield return new WaitForFixedUpdate();
            }
        }

        // Двигает через физику (Rigidbody.linearVelocity) - точно так же, как
        // CustomerQueueController двигает покупателей в очереди, поэтому пока
        // идёт по магазину, на него действует та же физика: гравитация,
        // столкновения, толчки игрока (PushStagger на секунду отпускает
        // управление, чтобы толчок реально сработал).
        //
        // Если есть NavMeshAgent (updatePosition/updateRotation у него
        // выключены - см. ShoppingRoutine) - направление берётся из его
        // desiredVelocity, он же сам обходит препятствия и других
        // покупателей. agent.nextPosition каждый кадр подтягивается к
        // реальной (физической) позиции - иначе агент "потеряет" покупателя.
        // Если агента нет - идёт по прямой, без обхода препятствий.
        private IEnumerator WalkTo(Transform customer, Rigidbody rb, Animator animator, PushStagger stagger, NavMeshAgent agent, Vector3 target)
        {
            bool useAgent = agent != null && agent.isOnNavMesh;
            if (useAgent)
                agent.SetDestination(target);

            while (customer != null)
            {
                if (useAgent && !agent.isOnNavMesh)
                    useAgent = false;

                if (useAgent)
                    agent.nextPosition = customer.position;

                bool reached = useAgent
                    ? !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance
                    : Vector3.Distance(customer.position, target) <= _stopDistance;

                if (reached) break;

                if (stagger != null && stagger.IsStaggered)
                {
                    SetWalking(animator, false);
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                Vector3 direction;
                float appliedSpeed;

                if (useAgent)
                {
                    Vector3 desired = agent.desiredVelocity;
                    desired.y = 0f;
                    float desiredMagnitude = desired.magnitude;

                    if (desiredMagnitude < 0.0001f)
                    {
                        yield return new WaitForFixedUpdate();
                        continue;
                    }

                    direction = desired / desiredMagnitude;
                    appliedSpeed = desiredMagnitude;
                }
                else
                {
                    Vector3 toTarget = target - customer.position;
                    toTarget.y = 0f;
                    float distance = toTarget.magnitude;

                    direction = toTarget / distance;
                    appliedSpeed = Mathf.Min(_walkSpeed, distance / Time.fixedDeltaTime);
                }

                Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion newRot = Quaternion.RotateTowards(customer.rotation, lookRot, _rotationSpeed * Time.fixedDeltaTime);
                Vector3 velocity = direction * appliedSpeed;

                SetWalking(animator, true);

                if (rb != null)
                {
                    rb.MoveRotation(newRot);
                    rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
                }
                else
                {
                    customer.rotation = newRot;
                    customer.position += velocity * Time.fixedDeltaTime;
                }

                yield return new WaitForFixedUpdate();
            }

            SetWalking(animator, false);

            if (customer != null && rb != null)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        private void SetWalking(Animator animator, bool isWalking)
        {
            if (animator != null)
                animator.SetBool(_isWalkingParam, isWalking);
        }
    }
}

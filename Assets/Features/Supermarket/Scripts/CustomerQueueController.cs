using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace GamePhone.Shop
{
    /// <summary>
    /// Очередь покупателей "для антуража". Никаких заранее расставленных точек
    /// для каждого места в очереди не нужно - есть только одна точка _queueFront
    /// (начало очереди, у кассы), а место каждого следующего покупателя в линии
    /// считается само: i-й по счёту встаёт на _queueSpacing метров позади
    /// предыдущего (по прямой назад от _queueFront). Покупателей может быть
    /// сколько угодно - очередь просто удлиняется сама.
    ///
    /// Если на модели покупателя есть Rigidbody - двигаем её через
    /// linearVelocity (как товары в ItemDragController), гравитация и
    /// столкновения работают по-настоящему: стоят на полу, не проходят сквозь
    /// стены. Если Rigidbody нет - двигаем напрямую через transform (старое
    /// поведение, на случай простых заглушек без физики).
    ///
    /// Скорость к цели ограничена так, чтобы не "перелетать" её за один
    /// физический шаг - иначе получается дёрганье туда-обратно возле точки.
    /// Поворачиваются не в сторону движения, а туда же, куда смотрит _queueFront
    /// (все в очереди лицом к кассе), либо туда же, куда смотрит точка маршрута
    /// выхода.
    ///
    /// При AdvanceQueue() первый идёт к _exitPoint (маршрут строит NavMesh сам,
    /// как при походе по полкам), а дальше падает сам под гравитацией - его
    /// убирает JobFailZone на дне пропасти (или, если физики нет, по старинке
    /// имитируем падение вручную). Тем же способом до _exitPoint идёт и вор,
    /// не заходя в очередь вообще (см. SendThiefToExit/EjectThief).
    ///
    /// Время от времени двое случайных свободных покупателей в очереди (кроме
    /// того, кого обслуживают) образуют пару и "разговаривают" - поворачиваются
    /// лицом друг к другу (см. TryStartConversation/ConversationRoutine), звук
    /// разговора звучит из точки между ними (3D, слышно только рядом), и ровно
    /// когда звук заканчивается - оба разворачиваются обратно как ни в чём не
    /// бывало. Пар может быть сразу несколько - каждая живёт независимо.
    /// </summary>
    public class CustomerQueueController : MonoBehaviour
    {
        [Tooltip("Начало очереди - место у кассы. Все остальные покупатели сами встают за ней друг за другом")]
        [SerializeField] private Transform _queueFront;
        [Tooltip("Расстояние между покупателями в очереди")]
        [SerializeField] private float _queueSpacing = 1f;
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private float _rotationSpeed = 540f;
        [Tooltip("Насколько близко к точке считается \"дошёл\" - останавливается и не дёргается")]
        [SerializeField] private float _stopDistance = 0.15f;
        [Tooltip("Имя bool-параметра в Animator (если он есть на модели покупателя), который включает анимацию ходьбы")]
        [SerializeField] private string _isWalkingParam = "isWalking";
        [Tooltip("Если первый в очереди не может дойти до кассы дольше стольки секунд (например его физически " +
                 "зажало толпой) - его силой ставят на место, чтобы касса не зависла навсегда, ожидая его")]
        [SerializeField] private float _frontStuckTimeout = 6f;

        [Header("Выход после обслуживания / для вора")]
        [Tooltip("Точка НА ПОЛУ у дверного проёма, к которой идёт покупатель - маршрут туда строит NavMesh сам, " +
                 "как при походе по полкам (см. CustomerShopper) - обходит препятствия и других покупателей. " +
                 "ВАЖНО: должна быть на запечённом NavMesh, иначе агент не сможет до неё дойти")]
        [SerializeField] private Transform _exitPoint;
        [Tooltip("Необязательно - точка ЗА порогом, уже над ямой/пропастью (вне NavMesh специально). После " +
                 "Exit Point покупатель делает до неё последний короткий шаг по прямой (без NavMesh) и дальше " +
                 "падает под гравитацией. Если не назначена - падения не будет, просто остановится у двери")]
        [SerializeField] private Transform _exitFallPoint;
        [SerializeField] private float _walkToDoorSpeed = 2f;
        [Tooltip("Без Rigidbody: насколько провалиться вниз вручную, прежде чем покупатель удалится")]
        [SerializeField] private float _fallDepth = 5f;
        [SerializeField] private float _fallSpeed = 4f;
        [Tooltip("С Rigidbody: сколько ждать после выхода, прежде чем удалить на всякий случай (если JobFailZone не поймала)")]
        [SerializeField] private float _fallbackDestroyDelay = 6f;

        [Header("Дверь (необязательно)")]
        [Tooltip("Общая дверь для входа и выхода - см. DoorController. Если не назначена, дверь не трогается")]
        [SerializeField] private DoorController _door;

        [Header("Болтовня в очереди (парами)")]
        [Tooltip("Варианты звука разговора - каждый раз для новой пары выбирается случайный")]
        [SerializeField] private AudioClip[] _turnAroundSounds;
        [Tooltip("Через сколько секунд (случайно, от и до) может образоваться новая пара разговаривающих")]
        [SerializeField] private float _minTurnInterval = 5f;
        [SerializeField] private float _maxTurnInterval = 15f;
        [Header("Слышимость разговора (3D-звук)")]
        [Tooltip("На каком расстоянии от говорящего звук ещё на полной громкости")]
        [SerializeField] private float _talkMinDistance = 2f;
        [Tooltip("На каком расстоянии от говорящего звук уже совсем не слышно")]
        [SerializeField] private float _talkMaxDistance = 10f;

        private class QueueCustomer
        {
            public Transform Transform;
            public Animator Animator;
            public Rigidbody Rigidbody;
            public PushStagger Stagger;
            public bool TurnedAround;
            // Кого сейчас "слушает" (второй в паре) - куда поворачиваться лицом вместо слота
            public Transform ConversationPartner;
            // Что покупатель набрал на полках (см. CustomerShopper) - именно это появится на кассе
            public List<ShopItemData> Order;
            // false, пока покупатель ещё идёт по NavMesh от полки к своему месту в
            // очереди (см. WalkInToSlot) - на это время FixedUpdate его не трогает,
            // им управляет сама эта корутина. true - как только физически дошёл,
            // дальше обычная построчная подстройка места по прямой (как раньше).
            public bool HasArrived;
        }

        // Срабатывает, когда первый в очереди доходит до кассы (_queueFront) -
        // передаёт список товаров, которые этот покупатель набрал на полках
        // (см. CustomerShopper). Используется CheckoutManager, чтобы товары
        // появлялись только когда покупатель реально подошёл, и именно те,
        // что он взял с полки.
        public event Action<List<ShopItemData>> OnCustomerArrivedAtRegister;

        // Нужна CustomerShopper - прежде чем встать в очередь (см. AddCustomer),
        // покупатель должен реально ДОЙТИ досюда по NavMesh (в обход полок), а не
        // телепортироваться сразу в список - очередь потом двигает по прямой,
        // только для мелкой подстройки места, обход препятствий не умеет.
        public Transform QueueFront => _queueFront;

        private readonly List<QueueCustomer> _customers = new();
        private QueueCustomer _notifiedFrontCustomer;

        // Отслеживаем именно застревание ПЕРВОГО в очереди - см. _frontStuckTimeout
        private QueueCustomer _frontStuckTrackedCustomer;
        private float _frontStuckTimer;

        private void Awake()
        {
            foreach (Transform child in transform)
            {
                _customers.Add(new QueueCustomer
                {
                    Transform = child,
                    Animator = child.GetComponentInChildren<Animator>(),
                    Rigidbody = child.GetComponentInChildren<Rigidbody>(),
                    Stagger = child.GetComponentInChildren<PushStagger>(),
                    // Уже расставлены в сцене вручную - считаем что они и так на месте
                    HasArrived = true
                });
            }
        }

        private void Start()
        {
            StartCoroutine(ConversationSpawnLoop());
        }

        private void FixedUpdate()
        {
            if (_queueFront == null) return;

            for (int i = 0; i < _customers.Count; i++)
            {
                QueueCustomer customer = _customers[i];

                // Ещё идёт по NavMesh от полки к своему месту (см. WalkInToSlot) -
                // тут его не трогаем, это делает сама корутина.
                if (!customer.HasArrived) continue;

                // Игрок только что толкнул - не перезаписываем скорость, даём физике сработать
                if (customer.Stagger != null && customer.Stagger.IsStaggered)
                {
                    SetWalking(customer.Animator, false);
                    continue;
                }

                // Первый в очереди - тот, кого сейчас обслуживают у кассы - всегда
                // смотрит вперёд, даже если только что был вовлечён в разговор.
                if (i == 0)
                {
                    customer.TurnedAround = false;
                    customer.ConversationPartner = null;

                    // Новый человек стал первым - счётчик "застрял" начинаем заново
                    if (_frontStuckTrackedCustomer != customer)
                    {
                        _frontStuckTrackedCustomer = customer;
                        _frontStuckTimer = 0f;
                    }
                }

                // i-е место в очереди - позади начала очереди на i * _queueSpacing,
                // по прямой назад от _queueFront. Слотов заранее расставлять не нужно.
                Vector3 slotPosition = _queueFront.position - _queueFront.forward * (_queueSpacing * i);
                Quaternion slotRotation = _queueFront.rotation;

                Vector3? lookAt = customer.TurnedAround && customer.ConversationPartner != null
                    ? customer.ConversationPartner.position
                    : null;

                bool isMoving = MoveTowards(customer.Transform, customer.Rigidbody, slotPosition, slotRotation, _moveSpeed, lookAt);

                // Первого в очереди могло физически зажать толпой - он честно
                // пытается дойти (isMoving остаётся true), но по факту не
                // приближается уже который кадр подряд. Не давая ему застрять
                // навсегда (а с ним - и всей кассе, которая его ждёт), через
                // _frontStuckTimeout секунд силой ставим точно на место.
                if (i == 0 && isMoving)
                {
                    _frontStuckTimer += Time.fixedDeltaTime;
                    if (_frontStuckTimer >= _frontStuckTimeout)
                    {
                        customer.Transform.position = slotPosition;
                        if (customer.Rigidbody != null)
                            customer.Rigidbody.linearVelocity = Vector3.zero;

                        isMoving = false;
                        _frontStuckTimer = 0f;
                    }
                }

                SetWalking(customer.Animator, isMoving);

                // Первый в очереди только что дошёл до кассы (раньше не отмечали
                // именно этого покупателя) - сообщаем наружу (CheckoutManager)
                // список товаров этого покупателя.
                if (i == 0 && !isMoving && _notifiedFrontCustomer != customer)
                {
                    _notifiedFrontCustomer = customer;
                    OnCustomerArrivedAtRegister?.Invoke(customer.Order);
                }
            }
        }

        // Раз в случайный промежуток пытается свести в пару двух случайных свободных
        // покупателей (не первого в очереди и не тех, кто уже в разговоре). Сам цикл
        // не блокируется на время разговора - поэтому пар может копиться сразу несколько.
        private IEnumerator ConversationSpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minTurnInterval, _maxTurnInterval));
                TryStartConversation();
            }
        }

        private void TryStartConversation()
        {
            var available = new List<QueueCustomer>();
            for (int i = 1; i < _customers.Count; i++)
            {
                if (_customers[i].HasArrived && !_customers[i].TurnedAround)
                    available.Add(_customers[i]);
            }

            if (available.Count < 2) return;

            QueueCustomer a = available[Random.Range(0, available.Count)];
            available.Remove(a);
            QueueCustomer b = available[Random.Range(0, available.Count)];

            StartCoroutine(ConversationRoutine(a, b));
        }

        // Разворачивает двоих лицом друг к другу, проигрывает звук из точки между
        // ними, и ровно когда звук доигрывает - оба разворачиваются обратно.
        private IEnumerator ConversationRoutine(QueueCustomer a, QueueCustomer b)
        {
            AudioClip clip = PickRandomClip(_turnAroundSounds);
            if (clip == null) yield break;

            a.TurnedAround = true;
            b.TurnedAround = true;
            a.ConversationPartner = b.Transform;
            b.ConversationPartner = a.Transform;

            Vector3 midpoint = (a.Transform.position + b.Transform.position) * 0.5f;
            PlaySoundAt(midpoint, clip);

            yield return new WaitForSeconds(clip.length);

            a.TurnedAround = false;
            b.TurnedAround = false;
            a.ConversationPartner = null;
            b.ConversationPartner = null;
        }

        private AudioClip PickRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        // Временный AudioSource прямо в точке говорящего - звук идёт именно от него,
        // а не из одной общей точки на всю очередь. 3D (spatialBlend = 1) - слышно
        // только рядом. Сам себя удаляет, когда клип доигрывает до конца.
        private void PlaySoundAt(Vector3 position, AudioClip clip)
        {
            var soundObject = new GameObject("Звук болтовни в очереди");
            soundObject.transform.position = position;

            var source = soundObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = _talkMinDistance;
            source.maxDistance = _talkMaxDistance;
            source.Play();

            Destroy(soundObject, clip.length);
        }

        // Вызывается CustomerShopper, когда покупатель обошёл полки - сразу же
        // занимает место в очереди (получает "номер" - позицию в списке), пока
        // ещё может стоять у дальней полки, и САМ идёт оттуда прямо к своему
        // месту по NavMesh (в обход полок/других покупателей) - см. WalkInToSlot.
        // Раньше все шли сначала к _queueFront (к кассе) и только потом
        // "рассасывались" по местам - все набирающие очередь одновременно
        // толпились у одной и той же точки. Теперь у каждого с самого начала
        // своя, отдельная цель - толпы у кассы больше не образуется.
        public void AddCustomer(Transform customer, List<ShopItemData> order = null)
        {
            customer.SetParent(transform);

            var queueCustomer = new QueueCustomer
            {
                Transform = customer,
                Animator = customer.GetComponentInChildren<Animator>(),
                Rigidbody = customer.GetComponentInChildren<Rigidbody>(),
                Stagger = customer.GetComponentInChildren<PushStagger>(),
                Order = order ?? new List<ShopItemData>(),
                HasArrived = false
            };

            _customers.Add(queueCustomer);
            StartCoroutine(WalkInToSlot(queueCustomer));
        }

        // Ведёт нового покупателя от того места, где он сейчас (может быть далеко,
        // ещё у дальней полки), напрямую к ЕГО РЕАЛЬНОМУ месту в очереди - через
        // NavMeshAgent (обходит полки и других покупателей), точно так же, как
        // CustomerShopper водит по полкам. Цель каждую итерацию пересчитывается
        // заново (_customers.IndexOf) - если кто-то впереди уже ушёл, пока этот
        // ещё идёт, место само подтянется поближе. Как только физически дошёл -
        // выставляет HasArrived = true и отдаёт покупателя обычной построчной
        // подстройке в FixedUpdate (для мелких движений по мере продвижения очереди).
        private IEnumerator WalkInToSlot(QueueCustomer queueCustomer)
        {
            Transform customer = queueCustomer.Transform;
            Rigidbody rb = queueCustomer.Rigidbody;
            Animator animator = queueCustomer.Animator;
            PushStagger stagger = queueCustomer.Stagger;

            if (customer == null || _queueFront == null)
            {
                if (customer != null) queueCustomer.HasArrived = true;
                yield break;
            }

            NavMeshAgent agent = customer.GetComponentInChildren<NavMeshAgent>();
            bool useAgent = false;

            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(customer.position);

                if (agent.isOnNavMesh)
                {
                    agent.speed = _moveSpeed;
                    agent.angularSpeed = _rotationSpeed;
                    agent.stoppingDistance = _stopDistance;
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    useAgent = true;
                }
                else
                {
                    agent.enabled = false;
                    agent = null;
                }
            }

            while (customer != null && _queueFront != null)
            {
                int index = _customers.IndexOf(queueCustomer);
                if (index < 0) break; // убрали из очереди (не должно случаться на этом этапе, но на всякий случай

                Vector3 slotPosition = _queueFront.position - _queueFront.forward * (_queueSpacing * index);

                if (useAgent && !agent.isOnNavMesh)
                    useAgent = false;

                bool reached = useAgent
                    ? !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance
                    : Vector3.Distance(customer.position, slotPosition) <= _stopDistance;

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
                    agent.nextPosition = customer.position;
                    agent.SetDestination(slotPosition);

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
                    Vector3 toTarget = slotPosition - customer.position;
                    toTarget.y = 0f;
                    float distance = toTarget.magnitude;

                    direction = toTarget / distance;
                    appliedSpeed = Mathf.Min(_moveSpeed, distance / Time.fixedDeltaTime);
                }

                FaceRotation(customer, rb, Quaternion.LookRotation(direction, Vector3.up));

                Vector3 velocity = direction * appliedSpeed;
                SetWalking(animator, true);

                if (rb != null)
                    rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
                else
                    customer.position += velocity * Time.fixedDeltaTime;

                yield return new WaitForFixedUpdate();
            }

            if (agent != null)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            if (customer != null)
            {
                SetWalking(animator, false);
                queueCustomer.HasArrived = true;
            }
        }

        // Вызывается CheckoutManager, когда покупателя у кассы обслужили
        public void AdvanceQueue()
        {
            if (_customers.Count == 0) return;

            QueueCustomer served = _customers[0];
            _customers.RemoveAt(0);

            if (_exitPoint != null)
                StartCoroutine(ExitRoutine(served.Transform, served.Animator, served.Rigidbody, served.Stagger));
            else
                Destroy(served.Transform.gameObject);
        }

        // Вызывается ShiftEndSequence в конце смены - отправляет всех, кто сейчас в
        // очереди (включая ещё идущих к своему месту по WalkInToSlot), к выходу тем
        // же способом, что и обычно обслуженного покупателя (см. ExitRoutine) -
        // никого реально не обслуживая, просто отпускает.
        public void ForceAllToExit()
        {
            var toSend = new List<QueueCustomer>(_customers);
            _customers.Clear();

            foreach (QueueCustomer c in toSend)
            {
                if (c.Transform == null) continue;

                if (_exitPoint != null)
                    StartCoroutine(ExitRoutine(c.Transform, c.Animator, c.Rigidbody, c.Stagger));
                else
                    Destroy(c.Transform.gameObject);
            }
        }

        // Вызывается CustomerShopper, когда покупатель решил уйти без оплаты (вор) -
        // идёт к _exitPoint тем же способом (NavMesh сам строит маршрут), что и
        // обслуженный покупатель, но НЕ через очередь. Если по дороге его поймали
        // (CustomerThief.IsThief стал false) - просто останавливается, дальше
        // "вышвыриванием" займётся EjectThief. Если дошёл до конца непойманным -
        // молча исчезает вместе с украденным (никакого падения в яму - это не
        // наказание, просто ушёл).
        public void SendThiefToExit(Transform customer, Animator animator, Rigidbody rb, PushStagger stagger)
        {
            StartCoroutine(ThiefWalkRoutine(customer, animator, rb, stagger));
        }

        // Дверь намеренно НЕ открывается сразу - иначе сам факт, что дверь открылась,
        // выдавал бы вора ещё на другом конце магазина. Открывается только когда
        // дошёл до порога (_exitPoint) - и то, только если не поймали по дороге.
        private IEnumerator ThiefWalkRoutine(Transform customer, Animator animator, Rigidbody rb, PushStagger stagger)
        {
            bool caught = false;

            yield return StartCoroutine(WalkToExitPoint(customer, rb, animator, stagger, () =>
            {
                CustomerThief thief = customer != null ? customer.GetComponent<CustomerThief>() : null;
                bool interrupted = thief == null || !thief.IsThief;
                if (interrupted) caught = true;
                return interrupted;
            }));

            // Поймали ещё по пути к порогу - дверь даже не открывали, дальше
            // вышвыриванием займётся CustomerThief через EjectThief/ExitRoutine.
            if (caught) yield break;

            bool doorOpened = false;
            if (_door != null)
            {
                yield return StartCoroutine(_door.RequestOpen());
                doorOpened = true;
            }

            // Дошёл до порога непойманным - последний короткий шаг через сам проём
            if (customer != null && _exitFallPoint != null)
            {
                bool moving = true;
                while (customer != null && moving)
                {
                    CustomerThief thief = customer.GetComponent<CustomerThief>();
                    if (thief == null || !thief.IsThief)
                    {
                        caught = true;
                        break;
                    }

                    moving = MoveTowards(customer, rb, _exitFallPoint.position, _exitFallPoint.rotation, _walkToDoorSpeed);
                    SetWalking(animator, moving);
                    yield return new WaitForFixedUpdate();
                }
            }

            if (customer != null)
            {
                SetWalking(animator, false);
                StopHorizontal(rb);
            }

            // Дверь закрываем в любом случае - и если поймали в проёме, и если ушёл
            if (doorOpened)
                yield return StartCoroutine(_door.ReleaseOpen());

            if (!caught && customer != null)
            {
                customer.GetComponent<CustomerThief>()?.NotifyEscaped();
                Destroy(customer.gameObject);
            }
        }

        // Вызывается CustomerThief, когда пойманного вора вышвыривают - идёт к той же
        // _exitPoint и падает в яму, что и обслуженный покупатель (переиспользует
        // ExitRoutine напрямую, минуя очередь - вор в ней никогда и не состоял).
        public void EjectThief(Transform customer, Animator animator, Rigidbody rb, PushStagger stagger)
        {
            if (_exitPoint != null)
                StartCoroutine(ExitRoutine(customer, animator, rb, stagger));
            else
                Destroy(customer.gameObject);
        }

        // Дверь открывается только когда покупатель дошёл до порога (_exitPoint),
        // а не сразу, как только его обслужили - иначе было бы слишком легко
        // "вычислить" происходящее просто по факту, что дверь открылась.
        private IEnumerator ExitRoutine(Transform customer, Animator animator, Rigidbody rb, PushStagger stagger)
        {
            yield return StartCoroutine(WalkToExitPoint(customer, rb, animator, stagger, null));

            bool doorOpened = false;
            if (_door != null)
            {
                yield return StartCoroutine(_door.RequestOpen());
                doorOpened = true;
            }

            // Последний короткий шаг - от порога (он на NavMesh) до точки над
            // ямой (она специально вне NavMesh) - тут NavMesh не нужен, просто
            // идём по прямой, как раньше делал весь маршрут целиком.
            if (customer != null && _exitFallPoint != null)
            {
                bool moving = true;
                while (customer != null && moving)
                {
                    moving = MoveTowards(customer, rb, _exitFallPoint.position, _exitFallPoint.rotation, _walkToDoorSpeed);
                    SetWalking(animator, moving);
                    yield return new WaitForFixedUpdate();
                }
            }

            if (customer != null)
            {
                SetWalking(animator, false);
                StopHorizontal(rb);
            }

            if (doorOpened)
                yield return StartCoroutine(_door.ReleaseOpen());

            if (customer == null) yield break;

            if (rb != null)
            {
                // Дальше падает сам под гравитацией - JobFailZone на дне пропасти его уберёт.
                // Подстраховка на случай, если зона не настроена или он куда-то не долетел.
                yield return new WaitForSeconds(_fallbackDestroyDelay);
                if (customer != null)
                    Destroy(customer.gameObject);
            }
            else
            {
                // Нет физики - имитируем падение вручную
                float targetY = customer.position.y - _fallDepth;
                while (customer != null && customer.position.y > targetY)
                {
                    customer.position += Vector3.down * _fallSpeed * Time.deltaTime;
                    yield return null;
                }

                if (customer != null)
                    Destroy(customer.gameObject);
            }
        }

        // Идёт к _exitPoint через NavMeshAgent, если он есть на покупателе (сам
        // строит маршрут и обходит препятствия/других покупателей - точно так же,
        // как CustomerShopper водит по полкам). Если агента нет или рядом нет
        // NavMesh - идёт по прямой, как раньше. isInterrupted (необязательно)
        // проверяется каждый физшаг - если вернёт true, останавливается раньше
        // (например, вора поймали по дороге - см. ThiefWalkRoutine).
        private IEnumerator WalkToExitPoint(Transform customer, Rigidbody rb, Animator animator, PushStagger stagger, Func<bool> isInterrupted)
        {
            if (_exitPoint == null || customer == null) yield break;

            NavMeshAgent agent = customer.GetComponentInChildren<NavMeshAgent>();
            bool useAgent = false;

            if (agent != null)
            {
                // Мог быть выключен/потерян с прошлого раза, когда покупатель им
                // пользовался (см. CustomerShopper) - Warp принудительно ставит
                // его на ближайшую точку NavMesh прямо сейчас.
                agent.enabled = true;
                agent.Warp(customer.position);

                if (agent.isOnNavMesh)
                {
                    agent.speed = _walkToDoorSpeed;
                    agent.angularSpeed = _rotationSpeed;
                    agent.stoppingDistance = _stopDistance;
                    // Сам transform агент не двигает - только считает путь, а
                    // реально двигает Rigidbody ниже (как и везде в этом проекте).
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    agent.SetDestination(_exitPoint.position);
                    useAgent = true;
                }
                else
                {
                    agent.enabled = false;
                    agent = null;
                }
            }

            while (customer != null)
            {
                if (isInterrupted != null && isInterrupted())
                    break;

                if (useAgent && !agent.isOnNavMesh)
                    useAgent = false;

                if (useAgent)
                    agent.nextPosition = customer.position;

                bool reached = useAgent
                    ? !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance
                    : Vector3.Distance(customer.position, _exitPoint.position) <= _stopDistance;

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
                    Vector3 toTarget = _exitPoint.position - customer.position;
                    toTarget.y = 0f;
                    float distance = toTarget.magnitude;

                    direction = toTarget / distance;
                    appliedSpeed = Mathf.Min(_walkToDoorSpeed, distance / Time.fixedDeltaTime);
                }

                FaceRotation(customer, rb, Quaternion.LookRotation(direction, Vector3.up));

                Vector3 velocity = direction * appliedSpeed;
                SetWalking(animator, true);

                if (rb != null)
                    rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
                else
                    customer.position += velocity * Time.fixedDeltaTime;

                yield return new WaitForFixedUpdate();
            }

            if (agent != null)
            {
                agent.ResetPath();
                agent.enabled = false;
            }
        }

        // Двигает customer к targetPosition - через физику (Rigidbody.linearVelocity), если она
        // есть, иначе напрямую через transform. Скорость ограничена так, чтобы не перелетать цель
        // за один физический шаг. Пока идёт - смотрит по направлению движения; как только
        // дошёл (< _stopDistance) - доворачивается точно как targetRotation, либо лицом
        // к lookAtOverride (позиция собеседника - см. ConversationRoutine), если она задана.
        // Возвращает true, если ещё не дошёл (в движении).
        private bool MoveTowards(Transform customer, Rigidbody rb, Vector3 targetPosition, Quaternion targetRotation, float speed, Vector3? lookAtOverride = null)
        {
            Vector3 toTarget = targetPosition - customer.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            bool isMoving = distance > _stopDistance;

            if (isMoving)
            {
                Vector3 direction = toTarget / distance;
                FaceRotation(customer, rb, Quaternion.LookRotation(direction, Vector3.up));

                // Скорость не больше той, что нужна чтобы доехать ровно до цели
                // за этот физшаг - без "перелёта" и последующего дёрганья назад.
                float appliedSpeed = Mathf.Min(speed, distance / Time.fixedDeltaTime);
                Vector3 horizontalVelocity = direction * appliedSpeed;

                if (rb != null)
                    rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
                else
                    customer.position += horizontalVelocity * Time.fixedDeltaTime;
            }
            else
            {
                Quaternion facing = targetRotation;

                if (lookAtOverride.HasValue)
                {
                    Vector3 toPartner = lookAtOverride.Value - customer.position;
                    toPartner.y = 0f;

                    if (toPartner.sqrMagnitude > 0.0001f)
                        facing = Quaternion.LookRotation(toPartner.normalized, Vector3.up);
                }

                FaceRotation(customer, rb, facing);

                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                    rb.angularVelocity = Vector3.zero;
                }
            }

            return isMoving;
        }

        private void StopHorizontal(Rigidbody rb)
        {
            if (rb != null)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        private void FaceRotation(Transform customer, Rigidbody rb, Quaternion targetRot)
        {
            Quaternion newRot = Quaternion.RotateTowards(customer.rotation, targetRot, _rotationSpeed * Time.fixedDeltaTime);

            if (rb != null)
                rb.MoveRotation(newRot);
            else
                customer.rotation = newRot;
        }

        private void SetWalking(Animator animator, bool isWalking)
        {
            if (animator != null)
                animator.SetBool(_isWalkingParam, isWalking);
        }
    }
}

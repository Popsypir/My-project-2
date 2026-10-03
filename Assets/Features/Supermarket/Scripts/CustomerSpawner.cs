using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace GamePhone.Shop
{
    /// <summary>
    /// Спавнит покупателей за дверью магазина: покупатель появляется под
    /// полом (в пустоте), дверь открывается, он поднимается на уровень пола -
    /// всё это время физика (Rigidbody) у него ВЫКЛЮЧЕНА (isKinematic = true),
    /// чтобы гравитация не тянула его вниз, пока скрипт одновременно двигает
    /// вверх (иначе он никогда не доходит до точки и виснет навсегда).
    ///
    /// После подъёма он ещё (тоже кинематически, без физики) делает шаг
    /// вперёд вглубь магазина (_walkInPoint) - это важно, потому что дверь
    /// общая с выходом, а у выхода рядом яма, в которую проваливаются
    /// обслуженные покупатели. Если включить физику прямо у двери - только
    /// вошедший может провалиться в ту же яму. Только отойдя от двери -
    /// дверь закрывается, и ТОЛЬКО ПОСЛЕ этого физика включается
    /// (isKinematic = false). Дальше покупателем никакие точки маршрута не
    /// управляют - он передаётся в CustomerShopper.StartShopping(), который
    /// ведёт его по полкам, а уже потом сам ставит в очередь.
    ///
    /// Спавн НЕ "разово N покупателей за смену" - вместо этого каждый тик
    /// (раз в случайный интервал) спавнер просто проверяет, сколько сейчас
    /// покупателей реально В МАГАЗИНЕ (см. _activeCustomers/CustomerPopulationTracker),
    /// и если меньше целевого числа - впускает ещё одного. Раз таймер спавна не
    /// завязан на то, когда именно кто-то ушёл, новый покупатель НЕ появляется
    /// тут же следом за ушедшим - будет ждать своего случайного интервала, как
    /// если бы просто шёл мимо по улице.
    ///
    /// Само целевое население при этом растёт по ходу смены (см.
    /// CurrentTargetPopulation) - вместе с TheftDifficultyManager (там растёт
    /// разрешённое число одновременных воров) магазин со временем становится
    /// всё многолюднее и напряжённее для охранника - без потолка, по-настоящему
    /// бесконечно, пока смена не кончится.
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        [Header("Кого спавнить")]
        [Tooltip("Разные модели/текстуры покупателей - при каждом спавне выбирается случайная")]
        [SerializeField] private GameObject[] _customerPrefabs;

        [Header("Спавн")]
        [Tooltip("Сколько покупателей держать в магазине одновременно в самом начале смены")]
        [SerializeField] private int _startingPopulation = 10;
        [Tooltip("На сколько покупателей увеличивать целевое население каждую стадию (растёт вместе с ворами - " +
                 "чем дольше идёт смена, тем больше народа в магазине и тем сложнее уследить за всеми)")]
        [SerializeField] private int _populationGrowthStep = 5;
        [Tooltip("Через сколько секунд целевое население увеличивается ещё на Population Growth Step")]
        [SerializeField] private float _populationGrowthInterval = 30f;
        [Tooltip("Как часто вообще проверять, не пора ли впустить ещё одного")]
        [SerializeField] private float _minSpawnInterval = 3f;
        [SerializeField] private float _maxSpawnInterval = 8f;
        [Tooltip("Необязательно - пока смена не активна (ещё не началась/уже закончилась), новых покупателей не впускаем")]
        [SerializeField] private CheckoutManager _checkout;

        private int _activeCustomers;
        private float _shiftStartTime = -1f;

        // Целевое население магазина прямо сейчас - растёт со временем (не потолок
        // на всю смену, а именно "сколько держать одновременно" - см. класс-докстринг).
        // Потолка намеренно нет - улетает по-настоящему бесконечно, как и воры.
        private int CurrentTargetPopulation
        {
            get
            {
                if (_shiftStartTime < 0f) return _startingPopulation;

                int stage = Mathf.FloorToInt((Time.time - _shiftStartTime) / Mathf.Max(1f, _populationGrowthInterval));
                return _startingPopulation + stage * _populationGrowthStep;
            }
        }

        [Header("3 точки маршрута появления")]
        [Tooltip("1) Точка под полом (в пустоте), откуда покупатель начинает подниматься")]
        [SerializeField] private Transform _spawnPointBelowFloor;
        [Tooltip("2) До какой точки подниматься - точка прямо на полу у двери")]
        [SerializeField] private Transform _riseToPoint;
        [Tooltip("3) Точка вглубь магазина, подальше от двери/ямы для выхода - только дойдя сюда, включаем физику")]
        [SerializeField] private Transform _walkInPoint;
        [SerializeField] private float _riseSpeed = 2f;
        [SerializeField] private float _walkInSpeed = 2f;
        [Tooltip("Имя bool-параметра в Animator, который включает анимацию ходьбы (на шаге к 3-й точке)")]
        [SerializeField] private string _isWalkingParam = "isWalking";

        [Header("Дверь (необязательно)")]
        [SerializeField] private DoorController _door;

        [Header("Куда покупатель передаётся после входа")]
        [SerializeField] private CustomerShopper _shopper;

        private void Start()
        {
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minSpawnInterval, _maxSpawnInterval));

                if (_checkout != null && !_checkout.IsShiftActive) continue;
                if (_activeCustomers >= CurrentTargetPopulation) continue;

                SpawnCustomer();
            }
        }

        // Вызывается CheckoutManager при старте смены - отсчёт стадий роста населения начинается заново
        public void ResetForNewShift()
        {
            _shiftStartTime = Time.time;
        }

        // Вызывает CustomerPopulationTracker, когда заспавненный им покупатель
        // пропадает из магазина - неважно, обслужили его, поймали вором или он
        // сам сбежал - лишь бы _activeCustomers всегда отражал реальное число.
        public void NotifyCustomerLeft()
        {
            _activeCustomers = Mathf.Max(0, _activeCustomers - 1);
        }

        private void SpawnCustomer()
        {
            if (_customerPrefabs == null || _customerPrefabs.Length == 0 || _spawnPointBelowFloor == null)
            {
                Debug.LogWarning("[CustomerSpawner] Не назначены Customer Prefabs или Spawn Point Below Floor", this);
                return;
            }

            GameObject prefab = _customerPrefabs[Random.Range(0, _customerPrefabs.Length)];
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, _spawnPointBelowFloor.position, _spawnPointBelowFloor.rotation);
            instance.AddComponent<CustomerPopulationTracker>().Initialize(this);
            _activeCustomers++;

            StartCoroutine(EnterRoutine(instance.transform));
        }

        private IEnumerator EnterRoutine(Transform customer)
        {
            Rigidbody rb = customer.GetComponentInChildren<Rigidbody>();
            Animator animator = customer.GetComponentInChildren<Animator>();
            NavMeshAgent agent = customer.GetComponentInChildren<NavMeshAgent>();

            if (_walkInPoint == null)
                Debug.LogWarning("[CustomerSpawner] Walk In Point не назначена - физика включится прямо у двери, " +
                                  "рядом с ямой для выхода, и покупатель может провалиться", this);

            // Пока поднимается - физика и анимация выключены: чистое
            // скриптовое движение, гравитация/анимация тут только мешали бы.
            if (rb != null)
                rb.isKinematic = true;
            if (animator != null)
                animator.enabled = false;

            // NavMeshAgent, если включён, сам пытается "прилипнуть" к ближайшей
            // точке NavMesh - а покупатель сейчас под полом, вне NavMesh. Выключаем
            // его на всё время подъёма/входа - включит и поставит на место сам
            // CustomerShopper, когда покупатель уже реально будет стоять в магазине.
            if (agent != null)
                agent.enabled = false;

            if (_door != null)
                yield return StartCoroutine(_door.RequestOpen());

            if (_riseToPoint != null)
            {
                while (customer != null && Vector3.Distance(customer.position, _riseToPoint.position) > 0.05f)
                {
                    customer.position = Vector3.MoveTowards(customer.position, _riseToPoint.position, _riseSpeed * Time.deltaTime);
                    yield return null;
                }
            }

            if (customer == null) yield break;

            // Шаг вглубь магазина - отходим от двери/ямы для выхода, прежде
            // чем включать физику. Анимацию тут уже включаем - это обычная
            // ходьба, а не "подъём из ниоткуда".
            if (_walkInPoint != null)
            {
                Vector3 toTarget = _walkInPoint.position - customer.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    customer.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

                if (animator != null)
                {
                    animator.enabled = true;
                    animator.SetBool(_isWalkingParam, true);
                }

                while (customer != null && Vector3.Distance(customer.position, _walkInPoint.position) > 0.05f)
                {
                    customer.position = Vector3.MoveTowards(customer.position, _walkInPoint.position, _walkInSpeed * Time.deltaTime);
                    yield return null;
                }

                if (animator != null)
                    animator.SetBool(_isWalkingParam, false);
            }

            if (customer == null) yield break;

            if (_door != null)
                yield return StartCoroutine(_door.ReleaseOpen());

            // Дверь закрылась - включаем физику (анимация уже включена выше).
            // Дальше персонажем никакие точки маршрута не рулят - им займётся
            // сама очередь.
            if (rb != null)
                rb.isKinematic = false;
            if (animator != null)
                animator.enabled = true;

            _shopper?.StartShopping(customer, rb, animator);
        }
    }
}

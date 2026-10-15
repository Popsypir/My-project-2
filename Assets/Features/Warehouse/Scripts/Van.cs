using System;
using System.Collections;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Фургон с ограниченной вместимостью. Под вечер приезжает к воротам (см.
    /// PostOfficeShift.EndShift), стоит и ждёт, пока игрок его грузит, потом
    /// уезжает и увозит ровно то, что в него влезло.
    ///
    /// Коробки укладываются по слотам - простая сетка внутри кузова. Как только
    /// слоты кончились, TryAccept возвращает false, и коробка остаётся у игрока.
    /// Принятая коробка становится дочерней кузову (см. Box.PlaceInVan:
    /// кинематическая и без коллайдера), поэтому спокойно едет вместе с ним.
    /// </summary>
    public class Van : MonoBehaviour
    {
        public enum OpenSide
        {
            Back,    // задняя стенка - там, где двери
            Front,
            Left,
            Right,
            Top      // открыт верх: коробки закидывают сверху
        }

        [Header("Кузов")]
        [Tooltip("Объём кузова: пустышка с BoxCollider и галочкой Is Trigger. Что лежит внутри него - то и уедет")]
        [SerializeField] private BoxCollider _cargoZone;
        [Tooltip("Куда цепляются коробки на время поездки. Не задашь - возьму сам фургон")]
        [SerializeField] private Transform _cargoRoot;

        [Header("Коллайдер по форме модели")]
        [Tooltip("Снять коллайдер прямо с модели, со всеми её выступами, вместо простых стенок. " +
                 "Годится, только если кузов внутри полый - иначе коробки будут лежать на крыше")]
        [SerializeField] private bool _colliderFromModel;
        [Tooltip("Меш машинки. Не назначишь - найду сам внутри фургона")]
        [SerializeField] private SkinnedMeshRenderer _modelMesh;
        [Tooltip("Пересобирать форму, пока едут двери. Иначе коллайдер остаётся с закрытыми створками, " +
                 "и в открытый кузов не войти - упираешься в невидимую дверь")]
        [SerializeField] private bool _colliderFollowsDoors = true;
        [Tooltip("Через сколько кадров пересобирать форму во время анимации. 1 - каждый кадр")]
        [SerializeField, Min(1)] private int _rebuildEveryFrames = 2;

        [Header("Стенки кузова")]
        [Tooltip("Построить невидимые стенки по краям кузова, чтобы коробки не проваливались сквозь пол " +
                 "и не вываливались с боков. Выключи, если у модели есть свои коллайдеры")]
        [SerializeField] private bool _buildWalls = true;
        [Tooltip("Толщина невидимых стенок")]
        [SerializeField] private float _wallThickness = 0.1f;
        [Tooltip("Какая сторона остаётся открытой, чтобы закидывать коробки")]
        [SerializeField] private OpenSide _openSide = OpenSide.Back;

        [Header("Маршрут")]
        [Tooltip("Откуда выезжает. Поставь пустышку далеко за воротами - там фургона не видно")]
        [SerializeField] private Transform _roadPoint;
        [Tooltip("Где паркуется под погрузку. Не задано - там, где фургон стоит в сцене")]
        [SerializeField] private Transform _parkPoint;
        [Tooltip("Скорость езды, метров в секунду")]
        [SerializeField] private float _driveSpeed = 6f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        [SerializeField] private float _turnSpeed = 180f;
        [Tooltip("Насколько близко к точке считать, что приехали")]
        [SerializeField] private float _arriveDistance = 0.2f;

        [Header("Ворота гаража")]
        [Tooltip("Ворота, которые поднимаются перед приездом и опускаются после отъезда. " +
                 "Нет ворот - фургон просто едет, ничего не ждёт")]
        [SerializeField] private GarageGate _gate;

        [Header("Двери")]
        [Tooltip("Клип открывания из модели машинки. Закрывание - этот же клип задом наперёд, второй рисовать не надо")]
        [SerializeField] private AnimationClip _doorClip;
        [Tooltip("К чему относится анимация - обычно корень модели машинки. Не назначишь - возьму сам фургон")]
        [SerializeField] private GameObject _doorRoot;
        [Tooltip("Во сколько раз быстрее играть. 1 - как в файле, 2 - вдвое быстрее")]
        [SerializeField] private float _doorSpeed = 1f;
        [Tooltip("Пауза после открывания дверей, прежде чем фургон посигналит")]
        [SerializeField] private float _pauseAfterDoors = 0.1f;

        [Header("Звук")]
        [Tooltip("Через него играют сигнал и шум двигателя")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Сигнал при парковке - чтобы игрок понял, что пора грузить")]
        [SerializeField] private AudioClip _hornClip;
        [Tooltip("Гудение двигателя на ходу. Зацикливать клип не нужно, включаю сам")]
        [SerializeField] private AudioClip _engineClip;

        /// <summary>
        /// Сколько посылок сейчас лежит в кузове. Считается каждый раз заново:
        /// пока фургон стоит, коробку можно и закинуть, и вытащить обратно.
        /// </summary>
        public int Count => CountBoxesInside();

        /// <summary>Сколько посылок увёз в прошлый раз.</summary>
        public int Shipped { get; private set; }

        /// <summary>Стоит на парковке и готов принимать коробки.</summary>
        public bool IsReadyToLoad { get; private set; }

        /// <summary>Приехал и встал под погрузку.</summary>
        public event Action OnArrived;

        /// <summary>Уехал окончательно - в этот момент уже скрыт.</summary>
        public event Action OnDeparted;

        // Поза парковки запоминается заранее: если _parkPoint не задан, фургон
        // паркуется туда, где его поставили в сцене - а сам он к этому моменту
        // уже уехал бы с этого места
        private Vector3 _parkPosition;
        private Quaternion _parkRotation;
        private Vector3 _roadPosition;
        private Quaternion _roadRotation;
        private bool _hasRoad;
        private bool _poseCaptured;

        /// <summary>Двери открыты - значит, фургон стоит под погрузкой.</summary>
        public bool DoorsOpen { get; private set; }

        private Mesh _bakedMesh;
        private MeshCollider _modelCollider;

        private void Awake()
        {
            if (_cargoRoot == null) _cargoRoot = transform;
            if (_doorRoot == null) _doorRoot = gameObject;
            CapturePose();
            BuildModelCollider();
            BuildWalls();

            // В файле модель сохранена с открытыми дверьми - на старте закрываем,
            // иначе фургон приедет уже нараспашку
            SampleDoors(0f);

            // Animator поверх нашей анимации каждый кадр ставил бы свою позу,
            // и двери дёргались бы между двумя анимациями
            var animator = _doorRoot.GetComponent<Animator>();
            if (animator != null && animator.enabled && animator.runtimeAnimatorController != null)
                Debug.LogWarning("[Фургон] На машинке висит Animator с контроллером - он будет спорить " +
                                 "за двери с этим скриптом. Выключи его или убери контроллер.", this);
        }

        private void OnEnable()
        {
            // Фургон, который стоит у ворот весь день (см. флажок "Van Arrives
            // At End" в PostOfficeShift), никуда не едет - и грузить его можно
            // сразу. Иначе он так и простоял бы, не принимая ни одной коробки.
            // Arrive() сам выключит это обратно на время пути.
            IsReadyToLoad = true;
        }

        private void OnDisable()
        {
            // Фургона на почте нет (ещё не приехал или уже уехал) - грузить нечего
            IsReadyToLoad = false;
        }

        private void CapturePose()
        {
            if (_poseCaptured) return;
            _poseCaptured = true;

            _parkPosition = _parkPoint != null ? _parkPoint.position : transform.position;
            _parkRotation = _parkPoint != null ? _parkPoint.rotation : transform.rotation;

            // Точки запоминаем один раз, при запуске, и дальше смотрим только на
            // эти числа. Иначе достаточно случайно положить точку внутрь самого
            // фургона - и она поедет вместе с ним: цель убегает от того, кто к
            // ней едет, и фургон либо не доезжает, либо приезжает не туда
            _hasRoad = _roadPoint != null;
            if (_hasRoad)
            {
                _roadPosition = _roadPoint.position;
                _roadRotation = _roadPoint.rotation;
            }

            WarnIfPointInsideVan(_roadPoint, "Road Point");
            WarnIfPointInsideVan(_parkPoint, "Park Point");
        }

        /// <summary>
        /// Перечитать точки маршрута. Зовём только когда фургон стоит на
        /// парковке - в этот момент даже точка, случайно положенная внутрь
        /// самого фургона, показывает верное место. Делать это на ходу нельзя:
        /// такая точка едет вместе с фургоном и цель убегала бы от него.
        /// </summary>
        private void RefreshRoute()
        {
            if (_roadPoint != null)
            {
                _roadPosition = _roadPoint.position;
                _roadRotation = _roadPoint.rotation;
                _hasRoad = true;
            }
            else _hasRoad = false;

            if (_parkPoint != null)
            {
                _parkPosition = _parkPoint.position;
                _parkRotation = _parkPoint.rotation;
            }
        }

        private void WarnIfPointInsideVan(Transform point, string field)
        {
            if (point == null || !point.IsChildOf(transform)) return;

            Debug.LogWarning($"[Фургон] {field} ({point.name}) лежит внутри самого фургона и ездит вместе с ним. " +
                             "Положение я запомнил при запуске, так что ехать будет правильно, но лучше вынести " +
                             "эту точку наружу, в корень сцены.", this);
        }

        /// <summary>
        /// Выехать из-за ворот и встать под погрузку. Дорожная точка не задана -
        /// просто появится на парковке, как было раньше.
        /// </summary>
        public void Arrive()
        {
            // До приезда фургон выключен, а выключенный объект не умеет крутить
            // корутины - поэтому сначала включаем, и только потом едем
            gameObject.SetActive(true);
            CapturePose();
            RefreshRoute();

            StopAllCoroutines();
            StartCoroutine(ArriveRoutine());
        }

        private IEnumerator ArriveRoutine()
        {
            IsReadyToLoad = false;

            if (_hasRoad)
            {
                transform.SetPositionAndRotation(_roadPosition, _roadRotation);

                // Ворота поднимаются ОДНОВРЕМЕННО с подъездом, а не до него:
                // ждать неподвижно, пока створка ползёт вверх, выглядит мёртво.
                // Корутину запускают сами ворота, чтобы она не оборвалась,
                // когда фургон выключится в конце поездки
                if (_gate != null) _gate.OpenNow();

                PlayEngine(true);
                yield return DriveTo(_parkPosition, _parkRotation);
                PlayEngine(false);

                // Подстраховка: если ворота почему-то ещё едут, дождёмся их.
                // Обычно подъём короче дороги, и ждать не приходится вовсе
                while (_gate != null && _gate.IsMoving) yield return null;
            }
            else if (_gate != null)
            {
                _gate.OpenNow();
                while (_gate.IsMoving) yield return null;
            }
            else
            {
                transform.SetPositionAndRotation(_parkPosition, _parkRotation);
            }

            // Сначала двери, потом разрешаем грузить: в закрытый кузов
            // коробка улетала бы сквозь дверь.
            // Клипа нет - ничего не ждём: фургон без анимации должен вести себя
            // ровно так, как вёл до её появления
            if (_doorClip != null)
            {
                yield return AnimateDoors(true);

                if (_pauseAfterDoors > 0f)
                    yield return new WaitForSeconds(_pauseAfterDoors);
            }
            else DoorsOpen = true;

            IsReadyToLoad = true;

            if (_audioSource != null && _hornClip != null)
                _audioSource.PlayOneShot(_hornClip);

            OnArrived?.Invoke();
        }

        /// <summary>
        /// Все посылки, которые сейчас лежат внутри кузова. Никаких слотов и
        /// никакого предела: сколько накидали, столько и есть.
        /// </summary>
        public System.Collections.Generic.List<Box> BoxesInside()
        {
            var result = new System.Collections.Generic.List<Box>();
            if (_cargoZone == null) return result;

            foreach (Box box in FindObjectsByType<Box>(FindObjectsInactive.Exclude))
                if (box != null && !box.IsHeld && !box.IsInVan && IsInsideCargo(box))
                    result.Add(box);

            return result;
        }

        /// <summary>Лежит ли посылка внутри кузова.</summary>
        public bool IsInsideCargo(Box box)
        {
            if (_cargoZone == null || box == null) return false;

            // Считаем по середине коробки, а не по касанию: коробка, которая
            // наполовину торчит наружу, честно считается невлезшей
            Vector3 center = box.TryGetComponent(out Collider col) ? col.bounds.center : box.transform.position;

            // Переводим точку в систему самой зоны, а не сверяемся с её
            // bounds. Во-первых, bounds берутся из физики и отстают, пока
            // фургон едет: коробки в кузове переставали бы считаться на ходу.
            // Во-вторых, bounds - это коробка по осям мира, и у повёрнутого
            // фургона она заметно больше настоящего кузова
            Vector3 local = _cargoZone.transform.InverseTransformPoint(center) - _cargoZone.center;
            Vector3 half = _cargoZone.size * 0.5f;

            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.y) <= half.y
                && Mathf.Abs(local.z) <= half.z;
        }

        private int CountBoxesInside()
        {
            if (_cargoZone == null) return 0;

            int count = 0;
            foreach (Box box in FindObjectsByType<Box>(FindObjectsInactive.Exclude))
                if (box != null && !box.IsHeld && !box.IsInVan && IsInsideCargo(box)) count++;

            return count;
        }

        /// <summary>
        /// Коллайдер точно по форме модели. Скелетный меш гнётся костями, и
        /// обычный MeshCollider к нему не прикрепить - галочка Generate Colliders
        /// при импорте создаёт пустышку, которая ничего не ловит. Поэтому мы
        /// запекаем текущую позу модели в обычный меш и уже его отдаём коллайдеру.
        ///
        /// Поза берётся та, что сейчас на модели - двери к этому моменту уже
        /// закрыты. Дальше коллайдер не шевелится: для машинки, у которой
        /// двигаются только створки, этого достаточно, а пересчитывать форму
        /// каждый кадр стоило бы дорого.
        /// </summary>
        private void BuildModelCollider()
        {
            if (!_colliderFromModel) return;

            if (_modelMesh == null) _modelMesh = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (_modelMesh == null)
            {
                Debug.LogWarning("[Фургон] Не нашёл меш машинки - коллайдер по форме модели не сделать", this);
                return;
            }

            // Коллайдер вешаем на отдельную пустышку с единичным масштабом.
            // Повесить его прямо на модель нельзя: запечённая форма уже учла
            // масштаб объекта, а коллайдер умножил бы его второй раз - у этой
            // машинки масштаб 260, и коллайдер раздулся бы до небес
            Transform holder = transform.Find("КоллайдерМодели");
            if (holder == null)
            {
                var go = new GameObject("КоллайдерМодели");
                holder = go.transform;
                holder.SetParent(transform, false);
            }

            holder.SetPositionAndRotation(_modelMesh.transform.position, _modelMesh.transform.rotation);
            holder.localScale = Vector3.one;

            _modelCollider = holder.GetComponent<MeshCollider>();
            if (_modelCollider == null) _modelCollider = holder.gameObject.AddComponent<MeshCollider>();
            _modelCollider.convex = false;   // выпуклый залил бы кузов изнутри

            RebuildModelCollider();

            // Пустой коллайдер от галочки Generate Colliders только мешает:
            // у скелетного меша он остаётся без формы и ничего не ловит
            foreach (var junk in GetComponentsInChildren<MeshCollider>(true))
                if (junk != _modelCollider && junk.sharedMesh == null) Destroy(junk);
        }

        /// <summary>
        /// Пересобрать форму коллайдера по текущей позе модели. Нужно всякий
        /// раз, когда модель шевелится: запечённая форма сама по себе
        /// неподвижна, и открытая дверь оставалась бы закрытой для физики -
        /// игрок упирался бы в невидимую створку прямо в проёме.
        /// </summary>
        public void RebuildModelCollider()
        {
            if (_modelCollider == null || _modelMesh == null) return;

            if (_bakedMesh == null) _bakedMesh = new Mesh { name = "Коллайдер " + _modelMesh.name };

            // Без второго параметра запекание отдаёт форму уже в мировом
            // размере - именно это нам и нужно для пустышки с масштабом 1
            _modelMesh.BakeMesh(_bakedMesh);

            // Переприсваиваем обязательно: тот же меш физика не пересчитает,
            // и форма осталась бы старой
            _modelCollider.sharedMesh = null;
            _modelCollider.sharedMesh = _bakedMesh;
        }

        private void OnDestroy()
        {
            if (_bakedMesh != null) Destroy(_bakedMesh);
        }

        /// <summary>
        /// Невидимые стенки по краям кузова        /// <summary>
        /// Невидимые стенки по краям кузова: пол, потолок и бока, кроме одной
        /// стороны - через неё игрок и закидывает коробки. Без них посылки
        /// проваливались бы сквозь модель, своих коллайдеров у неё нет.
        /// Стенки висят на кузове и едут вместе с ним.
        /// </summary>
        private void BuildWalls()
        {
            if (!_buildWalls || _cargoZone == null) return;

            var holder = new GameObject("СтенкиКузова");
            holder.transform.SetParent(_cargoZone.transform, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            Vector3 size = _cargoZone.size;
            Vector3 center = _cargoZone.center;
            float t = Mathf.Max(0.01f, _wallThickness);

            void Wall(string name, OpenSide side, Vector3 offset, Vector3 wallSize)
            {
                if (_openSide == side) return;

                var go = new GameObject(name);
                go.transform.SetParent(holder.transform, false);
                go.transform.localPosition = center + offset;
                go.AddComponent<BoxCollider>().size = wallSize;
            }

            Wall("Пол", OpenSide.Top, Vector3.down * (size.y * 0.5f), new Vector3(size.x, t, size.z));
            Wall("Потолок", OpenSide.Top, Vector3.up * (size.y * 0.5f), new Vector3(size.x, t, size.z));
            Wall("Левая", OpenSide.Left, Vector3.left * (size.x * 0.5f), new Vector3(t, size.y, size.z));
            Wall("Правая", OpenSide.Right, Vector3.right * (size.x * 0.5f), new Vector3(t, size.y, size.z));
            Wall("Задняя", OpenSide.Back, Vector3.back * (size.z * 0.5f), new Vector3(size.x, size.y, t));
            Wall("Передняя", OpenSide.Front, Vector3.forward * (size.z * 0.5f), new Vector3(size.x, size.y, t));

            // Пол нужен всегда: даже когда коробки закидывают сверху, вниз они
            // проваливаться не должны
            if (_openSide == OpenSide.Top)
            {
                var floor = new GameObject("Пол");
                floor.transform.SetParent(holder.transform, false);
                floor.transform.localPosition = center + Vector3.down * (size.y * 0.5f);
                floor.AddComponent<BoxCollider>().size = new Vector3(size.x, t, size.z);
            }
        }

        /// <summary>
        /// Уезжает - забирает ровно то, что успело влезть. Остальные коробки
        /// остаются лежать на почте до следующего дня.
        ///
        /// Число увезённых посылок возвращается сразу, не дожидаясь, пока фургон
        /// скроется за воротами: по нему считается итог смены.
        /// </summary>
        public int Depart()
        {
            RefreshRoute();

            // Что лежит в кузове на этот миг - то и уезжает. Коробки цепляем к
            // кузову и глушим им физику: живой Rigidbody считается в мировых
            // координатах и остался бы стоять на месте, пока фургон уезжает
            var cargo = BoxesInside();
            foreach (Box box in cargo) box.RideInVan(_cargoRoot);

            int taken = cargo.Count;
            Shipped = taken;
            IsReadyToLoad = false;

            if (!gameObject.activeInHierarchy)
            {
                // Фургон и так скрыт (например, смену завершили досрочно) -
                // ехать нечему, просто сообщаем, что он уехал
                OnDeparted?.Invoke();
                return taken;
            }

            StopAllCoroutines();
            StartCoroutine(DepartRoutine());
            return taken;
        }

        private IEnumerator DepartRoutine()
        {
            // Закрываемся до того, как тронуться: иначе фургон поехал бы с
            // распахнутыми дверьми и посылками наружу
            if (_doorClip != null) yield return AnimateDoors(false);
            else DoorsOpen = false;

            if (_hasRoad)
            {
                PlayEngine(true);

                // Створка пошла вниз, пока фургон отъезжает - у неё своя
                // задержка перед закрытием, так что на крышу она не рухнет
                if (_gate != null) _gate.CloseNow();

                yield return DriveTo(_roadPosition, _roadRotation);
                PlayEngine(false);
            }
            else if (_gate != null) _gate.CloseNow();

            OnDeparted?.Invoke();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Открывает или закрывает двери. Клип один - закрывание это он же,
        /// проигранный задом наперёд, поэтому второй клип рисовать не надо.
        ///
        /// Кадры клипа мы расставляем сами, без Animator и без контроллера
        /// состояний: так в редакторе нечего настраивать, достаточно перетащить
        /// клип в поле. Заодно это позволяет гнать анимацию в любую сторону и с
        /// любой скоростью.
        /// </summary>
        private IEnumerator AnimateDoors(bool opening)
        {
            if (_doorClip == null || _doorRoot == null)
            {
                DoorsOpen = opening;   // дверей нет - считаем, что всё уже как надо
                yield break;
            }

            float length = _doorClip.length;
            float speed = Mathf.Max(0.01f, _doorSpeed);
            float t = 0f;

            int frame = 0;
            while (t < length)
            {
                t += Time.deltaTime * speed;
                SampleDoors(opening ? Mathf.Min(t, length) : Mathf.Max(0f, length - t));

                // Коллайдер догоняет створки, иначе в проёме останется
                // невидимая дверь
                if (_colliderFollowsDoors && ++frame % Mathf.Max(1, _rebuildEveryFrames) == 0)
                    RebuildModelCollider();

                yield return null;
            }

            SampleDoors(opening ? length : 0f);
            DoorsOpen = opening;

            // В конце - обязательно точно по финальной позе
            RebuildModelCollider();
        }

        /// <summary>Поставить двери в позу на заданной секунде клипа.</summary>
        public void SampleDoors(float time)
        {
            if (_doorClip == null || _doorRoot == null) return;

            _doorClip.SampleAnimation(_doorRoot, Mathf.Clamp(time, 0f, _doorClip.length));
        }

        /// <summary>Длина клипа дверей в секундах с учётом выставленной скорости.</summary>
        public float DoorDuration => _doorClip == null ? 0f : _doorClip.length / Mathf.Max(0.01f, _doorSpeed);

        // Едет по прямой к точке, поворачиваясь носом по ходу движения,
        // и в самом конце доворачивается точно по нужному углу
        private IEnumerator DriveTo(Vector3 position, Quaternion rotation)
        {
            while (true)
            {
                Vector3 delta = position - transform.position;
                if (delta.sqrMagnitude <= _arriveDistance * _arriveDistance) break;

                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(flat);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(transform.position, position, _driveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = position;

            // Доворот на месте - иначе фургон встанет под тем случайным углом,
            // каким его застал последний кадр движения
            while (Quaternion.Angle(transform.rotation, rotation) > 0.5f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, _turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = rotation;
        }

        private void PlayEngine(bool on)
        {
            if (_audioSource == null || _engineClip == null) return;

            if (on)
            {
                _audioSource.clip = _engineClip;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            else if (_audioSource.clip == _engineClip)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
                _audioSource.clip = null;
            }
        }


        private void OnDrawGizmosSelected()
        {
            // Объём кузова: что окажется внутри этой коробки, то фургон и увезёт
            if (_cargoZone != null)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
                Gizmos.matrix = _cargoZone.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(_cargoZone.center, _cargoZone.size);
                Gizmos.matrix = Matrix4x4.identity;
            }

            // Путь, по которому поедет фургон
            if (_roadPoint != null)
            {
                Vector3 park = _parkPoint != null ? _parkPoint.position : transform.position;
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_roadPoint.position, park);
                Gizmos.DrawWireSphere(_roadPoint.position, 0.5f);
                Gizmos.DrawWireSphere(park, 0.5f);
            }
        }
    }
}

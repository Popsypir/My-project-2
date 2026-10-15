using UnityEngine;

namespace GamePhone.World
{
    /// <summary>
    /// Переключение каналов ударом: игрок подходит к телевизору, жмёт левую
    /// кнопку мыши - сверху влетает лапа (см. TvPawSwipe), телевизор в момент
    /// удара приседает и пружинит обратно, канал меняется (см. TvSet).
    ///
    /// Бить можно и по экрану, и по корпусу - это разные модели, потому что
    /// иначе на них криво ложились текстуры. Задняя стенка не считается: она
    /// придвинута к стене, и игрок её всё равно не видит.
    ///
    /// Лапа каждый раз попадает в случайное место, а не в одну и ту же точку -
    /// иначе удары выглядели бы как под копирку.
    ///
    /// Сам телевизор собран из нескольких мешей внутри общей модели почты, и
    /// общего родителя у них нет. Поэтому при запуске скрипт заводит себе
    /// пустышку в основании телевизора и подвешивает части под неё - масштаб
    /// тогда считается от пола, и телевизор приседает, а не раздувается из
    /// центра. Пустышка живёт только в игре, саму сцену это не меняет.
    /// </summary>
    public class TvPunch : MonoBehaviour
    {
        [Header("Что бьём")]
        [Tooltip("Телевизор, у которого переключаем каналы. Не назначишь - возьму с этого же объекта")]
        [SerializeField] private TvSet _tv;
        [Tooltip("Части телевизора - корпус и экран. Плющатся вместе")]
        [SerializeField] private Transform[] _parts;
        [Tooltip("По чему можно попасть лапой: коллайдеры экрана и корпуса")]
        [SerializeField] private Collider[] _hitAreas;
        [Tooltip("Экран - по нему понятно, где у телевизора перёд, а где зад")]
        [SerializeField] private Transform _screen;

        [Header("Кто бьёт")]
        [Tooltip("Глаза игрока: из их центра проверяется, смотрит ли он на телевизор. Не назначишь - возьму главную камеру")]
        [SerializeField] private Camera _camera;
        [Tooltip("С какого расстояния достаёт лапа")]
        [SerializeField] private float _reach = 3f;
        [Tooltip("Пока игрок несёт посылку, бить нельзя - иначе клик делал бы два дела разом")]
        [SerializeField] private GamePhone.Warehouse.BoxCarrier _carrier;

        [Header("Лапа")]
        [Tooltip("Замах лапой. Оставишь пустым - канал будет переключаться сразу, без анимации")]
        [SerializeField] private TvPawSwipe _paw;
        [Tooltip("На сколько случайная точка отступает от краёв детали, чтобы лапа не била в самый угол")]
        [SerializeField, Range(0f, 0.45f)] private float _edgeMargin = 0.18f;

        [Header("Как плющится")]
        [Tooltip("Насколько приседает по высоте в самый сильный момент. 0.15 - на 15%, " +
                 "1 - схлопывается в блин, а дальше можно и не останавливаться")]
        [SerializeField] private float _squash = 0.16f;
        [Tooltip("Насколько при этом раздаётся вширь. 1 - становится вдвое шире")]
        [SerializeField] private float _stretch = 0.08f;
        [Tooltip("Сколько длится приседание с отпружиниванием")]
        [SerializeField] private float _squashDuration = 0.45f;
        [Tooltip("Сколько раз качнётся, пока успокоится")]
        [SerializeField] private float _wobbles = 2.5f;

        [Header("Звук")]
        [Tooltip("Через него играет звук удара")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Звук удара по телевизору")]
        [SerializeField] private AudioClip _punchClip;

        /// <summary>Удар сейчас проигрывается - второй раз бить нельзя.</summary>
        public bool IsPunching => _squashTime < _squashDuration;

        /// <summary>Куда пришёлся последний удар.</summary>
        public Vector3 LastHitPoint { get; private set; }

        /// <summary>По какой детали пришёлся последний удар - по экрану или по корпусу.</summary>
        public Collider LastHitArea { get; private set; }

        private Transform _pivot;
        private float _squashTime = float.MaxValue;
        private bool _channelSwitched;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            if (_tv == null) _tv = GetComponent<TvSet>();
            if (_carrier == null) _carrier = FindAnyObjectByType<GamePhone.Warehouse.BoxCarrier>();

            BuildPivot();
        }

        // Пустышка в основании телевизора, под неё уходят все его части
        private void BuildPivot()
        {
            if (_parts == null || _parts.Length == 0) return;

            bool hasBounds = false;
            Bounds bounds = new Bounds();

            foreach (Transform part in _parts)
            {
                if (part == null) continue;
                var r = part.GetComponent<Renderer>();
                if (r == null) continue;

                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!hasBounds) return;

            var go = new GameObject("ТелевизорПружина");
            _pivot = go.transform;
            _pivot.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            _pivot.rotation = Quaternion.identity;

            // Родителя берём общего с первой частью, чтобы телевизор не уехал,
            // если вся модель почты стоит не в начале координат
            Transform firstParent = null;
            foreach (Transform part in _parts)
                if (part != null) { firstParent = part.parent; break; }
            if (firstParent != null) _pivot.SetParent(firstParent, true);

            foreach (Transform part in _parts)
                if (part != null) part.SetParent(_pivot, true);
        }

        /// <summary>
        /// Куда смотрит телевизор. Считается от середины всего телевизора к
        /// экрану: экран всегда на лицевой стороне, так что эта стрелка и есть
        /// "перёд". Поворачивай телевизор в сцене как хочешь - само сойдётся.
        /// </summary>
        private Vector3 Front
        {
            get
            {
                Bounds all = CombinedBounds(out bool ok);
                if (!ok || _screen == null) return transform.forward;

                Vector3 front = BoundsOf(_screen).center - all.center;
                front.y = 0f;

                return front.sqrMagnitude > 0.0001f ? front.normalized : transform.forward;
            }
        }

        private Bounds CombinedBounds(out bool ok)
        {
            ok = false;
            Bounds result = new Bounds();

            if (_hitAreas == null) return result;

            foreach (Collider c in _hitAreas)
            {
                if (c == null) continue;
                if (!ok) { result = c.bounds; ok = true; }
                else result.Encapsulate(c.bounds);
            }

            return result;
        }

        private static Bounds BoundsOf(Transform t)
        {
            var col = t.GetComponent<Collider>();
            if (col != null) return col.bounds;

            var rend = t.GetComponent<Renderer>();
            return rend != null ? rend.bounds : new Bounds(t.position, Vector3.one * 0.1f);
        }

        private void Update()
        {
            TickSquash(Time.deltaTime);

            if (_paw != null && _paw.IsPlaying) return;   // лапа ещё летит
            if (!Input.GetMouseButtonDown(0)) return;
            if (!CanPunch()) return;
            if (!AimingAtTv()) return;

            Punch();
        }

        private bool CanPunch()
        {
            if (IsPunching) return false;
            // Открыт телефон, идёт набор текста, висит окно поверх экрана
            if (GameplayInputLock.Blocked) return false;
            // В лапах посылка - сначала освободи их
            if (_carrier != null && _carrier.IsCarrying) return false;
            return true;
        }

        /// <summary>Смотрит ли игрок на телевизор и достаёт ли до него.</summary>
        private bool AimingAtTv()
        {
            if (_camera == null || _hitAreas == null) return false;

            // Луч из центра экрана: мышь в игре приклеена к центру, как прицел
            Ray ray = _camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            Vector3 front = Front;

            foreach (Collider area in _hitAreas)
            {
                if (area == null || !area.enabled) continue;
                if (!area.Raycast(ray, out RaycastHit hit, _reach)) continue;

                // Задняя стенка не в счёт: она у стены, игрок её не видит
                if (Vector3.Dot(hit.normal, front) < -0.5f) continue;

                return true;
            }

            return false;
        }

        /// <summary>Ударить по телевизору. Можно дёрнуть и снаружи - например, из теста.</summary>
        public void Punch()
        {
            _squashTime = 0f;
            _channelSwitched = false;

            Vector3 viewer = _camera != null ? _camera.transform.position : transform.position;

            if (_paw != null && TryPickHitPoint(viewer, out Vector3 point, out Vector3 normal))
            {
                LastHitPoint = point;
                _paw.Play(viewer, point, normal);
            }
            else
            {
                SwitchChannel();   // лапы нет или бить не по чему - переключаем сразу
            }
        }

        /// <summary>
        /// Случайная точка на одной из деталей телевизора, с той стороны, с
        /// которой стоит игрок: удар достаётся то экрану, то корпусу, и каждый
        /// раз в новое место.
        /// </summary>
        public bool TryPickHitPoint(Vector3 viewerPosition, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            if (_hitAreas == null || _hitAreas.Length == 0) return false;

            // Деталь выбираем случайно - удар достаётся то экрану, то корпусу
            var candidates = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in _hitAreas)
                if (c != null && c.enabled) candidates.Add(c);
            if (candidates.Count == 0) return false;

            Collider area = candidates[Random.Range(0, candidates.Count)];
            LastHitArea = area;
            Bounds b = area.bounds;
            Vector3 front = Front;

            Vector3 toViewer = viewerPosition - b.center;
            toViewer.y = 0f;
            toViewer = toViewer.sqrMagnitude > 0.0001f ? toViewer.normalized : front;
            // Игрок зашёл за телевизор - всё равно бьём спереди, а не в зад
            if (Vector3.Dot(toViewer, front) < 0f) toViewer = front;

            // Стороны коробки, по которым можно попасть: лицевая, верх и бока.
            // Зад отпадает сам - он смотрит от игрока. Низ не берём: туда и
            // лапой не подлезть, телевизор стоит на тумбе.
            Vector3 side = Vector3.Cross(Vector3.up, front).normalized;
            var faces = new Vector3[] { front, Vector3.up, side, -side };

            var allowed = new System.Collections.Generic.List<Vector3>();
            foreach (Vector3 f in faces)
            {
                if (Vector3.Dot(f, front) < -0.5f) continue;        // задняя стенка
                if (Vector3.Dot(f, toViewer) < 0.15f) continue;     // сторона отвёрнута от игрока
                allowed.Add(f);
            }
            if (allowed.Count == 0) allowed.Add(front);

            Vector3 faceNormal = allowed[Random.Range(0, allowed.Count)];

            // Точка на выбранной стороне: по самой нормали выходим на поверхность,
            // а вдоль двух других осей гуляем случайно, отступив от краёв
            Vector3 u = Vector3.Cross(faceNormal, Vector3.up);
            if (u.sqrMagnitude < 0.001f) u = Vector3.Cross(faceNormal, front);
            u = u.normalized;
            Vector3 v = Vector3.Cross(faceNormal, u).normalized;

            float margin = Mathf.Clamp01(1f - _edgeMargin * 2f);
            Vector3 half = b.extents;
            float alongNormal = Mathf.Abs(Vector3.Dot(half, Abs(faceNormal)));
            float alongU = Mathf.Abs(Vector3.Dot(half, Abs(u))) * margin;
            float alongV = Mathf.Abs(Vector3.Dot(half, Abs(v))) * margin;

            point = b.center
                  + faceNormal * alongNormal
                  + u * Random.Range(-alongU, alongU)
                  + v * Random.Range(-alongV, alongV);
            normal = faceNormal;
            return true;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void TickSquash(float deltaTime)
        {
            if (_paw != null && _paw.IsPlaying && !_channelSwitched)
            {
                // Канал меняется ровно в тот момент, когда лапа коснулась телевизора,
                // а не когда она только начала падать
                if (_paw.HitProgress >= 1f) SwitchChannel();
                return;
            }

            if (_squashTime >= _squashDuration) return;

            _squashTime += deltaTime;
            ApplySquash(Mathf.Clamp01(_squashTime / _squashDuration));
        }

        private void SwitchChannel()
        {
            if (_channelSwitched) return;
            _channelSwitched = true;

            _squashTime = 0f;   // приседание начинается от касания

            if (_tv != null) _tv.NextChannel();

            if (_audioSource != null && _punchClip != null)
                _audioSource.PlayOneShot(_punchClip);
        }

        // Резко присел и затухающими качками вернулся - как желе
        private void ApplySquash(float k)
        {
            if (_pivot == null) return;

            float damping = 1f - k;
            float wave = Mathf.Cos(k * Mathf.PI * 2f * _wobbles) * damping * damping;

            // Ниже нуля не опускаемся: отрицательный масштаб вывернул бы
            // телевизор наизнанку. Ноль - это и есть "сжался до атомов"
            float y = Mathf.Max(0f, 1f - _squash * wave);
            float xz = Mathf.Max(0f, 1f + _stretch * wave);

            _pivot.localScale = new Vector3(xz, y, xz);
        }

        private void OnDrawGizmosSelected()
        {
            if (_hitAreas == null) return;

            Gizmos.color = Color.cyan;
            foreach (Collider area in _hitAreas)
                if (area != null) Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);

            Bounds all = CombinedBounds(out bool ok);
            if (!ok) return;

            Gizmos.DrawWireSphere(all.center, _reach);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(all.center, Front);   // куда смотрит телевизор
        }
    }
}

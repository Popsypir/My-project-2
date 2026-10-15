using System.Collections;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Один посетитель почты. Два типа (см. Kind):
    /// - Sender приносит посылку: доходит до стойки, ставит коробку на неё и уходит.
    /// - Receiver пришёл ЗА посылкой: ждёт у стойки, пока игрок не вручит ему
    ///   коробку (см. BoxCarrier), и уходит вместе с ней. Не дождался за
    ///   _patience секунд - уходит ни с чем (см. PostOfficeShift.Stats).
    ///
    /// Ходит по прямой между точками (в сцене нет NavMesh) - поэтому NavMeshAgent
    /// на префабе выключается, а Rigidbody держим кинематическим: гравитация и
    /// скриптовое перемещение иначе дерутся друг с другом, и NPC повисает,
    /// не доходя до точки. Коллайдер при этом остаётся - сквозь посетителя
    /// пройти нельзя.
    /// </summary>
    public class PostNpc : MonoBehaviour
    {
        public enum NpcKind { Sender, Receiver }

        [SerializeField] private float _walkSpeed = 2.2f;
        [SerializeField] private float _stopDistance = 0.15f;
        [SerializeField] private string _isWalkingParam = "isWalking";
        [Tooltip("Сколько секунд Receiver ждёт свою посылку, прежде чем уйти ни с чем. " +
                 "0 - ждёт сколько угодно. Sender ждёт всегда: он не уйдёт, пока у него не заберут посылку")]
        [SerializeField] private float _patience = 0f;
        [Tooltip("Через сколько секунд уходит, если его посылку увезли и отдавать уже нечего. " +
                 "Небольшая задержка нужна, чтобы он не разворачивался от случайной заминки")]
        [SerializeField] private float _giveUpWhenParcelGone = 2f;

        public NpcKind Kind { get; private set; }

        /// <summary>Receiver стоит у стойки и ждёт посылку - только такому можно вручить коробку.</summary>
        public bool WantsParcel => Kind == NpcKind.Receiver && _atCounter && !_served;

        /// <summary>Sender стоит у стойки и держит посылку - её можно у него забрать.</summary>
        public bool OffersParcel => Kind == NpcKind.Sender && _atCounter && !_served && _carriedBox != null;

        [Tooltip("Где посетитель держит посылку - в его локальных координатах. " +
                 "Высота подобрана так, чтобы коробка оказалась над столешницей")]
        [SerializeField] private Vector3 _carryLocalPosition = new Vector3(0f, 2.7f, 2.2f);

        [Header("Реплика над головой")]
        [SerializeField] private TMPro.TMP_FontAsset _nameTagFont;
        [SerializeField] private float _nameTagSize = 2.4f;
        [Tooltip("На сколько метров реплика висит выше макушки")]
        [SerializeField] private float _nameTagHeight = 0.3f;
        [Tooltip("Что говорит тот, кто ПРИНЁС посылку - дальше имя с её бирки")]
        [SerializeField] private string _senderPhrase = "Посылка для";
        [Tooltip("Что говорит тот, кто пришёл ЗА посылкой - дальше его имя")]
        [SerializeField] private string _receiverPhrase = "Я за посылкой,";

        private TMPro.TextMeshPro _nameTag;
        private float _feetOffset;
        private int _queueSlot;
        private PostOfficeShift _shift;
        private Transform _counterPoint;
        private Transform _exitPoint;
        private Transform _carryPoint;
        private Box _carriedBox;
        private Animator _animator;
        private bool _atCounter;
        private bool _served;
        private bool _leaving;
        private Coroutine _moveRoutine; // Ссылка на текущую корутину движения для перезапуска
        private bool _slotReached;      // Дошли до своего места в очереди и стоим на нём
        private bool _falling;          // Провалился в люк и летит вниз

        public void Begin(PostOfficeShift shift, NpcKind kind, Transform counterPoint, Transform exitPoint, Box carriedBox,
                          string requestedName = null)
        {
            _shift = shift;
            Kind = kind;
            RequestedName = requestedName;
            _counterPoint = counterPoint;
            _exitPoint = exitPoint;
            _animator = GetComponentInChildren<Animator>();

            var agent = GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            var rb = GetComponentInChildren<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // У модели посетителя точка отсчёта не в ногах, а примерно на уровне
            // колен: если просто поставить её в точку появления, он окажется
            // наполовину в полу. Запоминаем, насколько низ модели ниже корня, и
            // дальше всегда ставим его ровно на пол (см. SnapToGround).
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null) _feetOffset = transform.position.y - rend.bounds.min.y;
            SnapToGround();

            // Посылку посетитель держит на вытянутых руках НАД столешницей
            // (её верх примерно на 1.6 м): ниже она тонула бы в прилавке -
            // игрок не увидел бы её и не смог бы взять через стойку.
            _carryPoint = new GameObject("CarryPoint").transform;
            _carryPoint.SetParent(transform, false);
            _carryPoint.localPosition = _carryLocalPosition;

            if (carriedBox != null)
            {
                _carriedBox = carriedBox;
                _carriedBox.PickUp(_carryPoint);
            }

            // Обоим видно, на чьё имя посылка: принесший называет имя с бирки,
            // пришедший за посылкой - своё собственное.
            string spoken = kind == NpcKind.Sender
                ? (_carriedBox != null ? _carriedBox.RecipientName : null)
                : requestedName;
            if (!string.IsNullOrEmpty(spoken))
                CreateNameTag(kind == NpcKind.Sender ? _senderPhrase + "\n" + spoken
                                                     : _receiverPhrase + "\n" + spoken);

            StartCoroutine(VisitRoutine());
        }

        /// <summary>Имя посетителя - он пришёл за посылкой именно с этим именем на бирке.</summary>
        public string RequestedName { get; private set; }

        /// <summary>
        /// Игрок вручает посылку. Чужую посылку посетитель не возьмёт - имя на
        /// бирке должно совпасть с его собственным (см. RequestedName).
        /// </summary>
        public bool GiveParcel(Box box)
        {
            if (!WantsParcel || box == null) return false;
            if (!string.IsNullOrEmpty(RequestedName) && box.RecipientName != RequestedName) return false;

            _served = true;
            _carriedBox = box;
            box.PickUp(_carryPoint);
            _shift?.NotifyReceiverServed();
            return true;
        }

        /// <summary>
        /// Игрок забирает посылку из рук посетителя. Возвращает её же (или null,
        /// если этот посетитель ничего не предлагает) - только после этого он уходит.
        /// </summary>
        public Box TakeParcel()
        {
            if (!OffersParcel) return null;

            Box box = _carriedBox;
            _carriedBox = null;
            _served = true;
            _shift?.NotifyParcelDelivered();
            return box;
        }

        // Имя над головой: по нему игрок понимает, какую посылку искать.
        // Табличка каждый кадр разворачивается к камере (см. LateUpdate).
        private void CreateNameTag(string name)
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);

            // Высоту берём от реальной макушки модели, а не фиксированным числом:
            // у персонажа своя точка отсчёта и свой масштаб, и при жёстком значении
            // табличка оказывалась внутри тела, где её попросту не видно.
            var rend = GetComponentInChildren<Renderer>();
            float headTop = rend != null ? rend.bounds.max.y : transform.position.y + 3.7f;
            go.transform.position = new Vector3(transform.position.x, headTop + _nameTagHeight, transform.position.z);

            _nameTag = go.AddComponent<TMPro.TextMeshPro>();
            if (_nameTagFont != null) _nameTag.font = _nameTagFont;
            _nameTag.text = name;
            _nameTag.fontSize = _nameTagSize;
            _nameTag.color = Color.white;
            _nameTag.alignment = TMPro.TextAlignmentOptions.Center;
            _nameTag.rectTransform.sizeDelta = new Vector2(7f, 2.4f);
            _nameTag.textWrappingMode = TMPro.TextWrappingModes.Normal;
        }

        // Ставит посетителя ногами ровно на пол под ним. Свои собственные
        // коллайдеры пропускаем - иначе луч упирается в его же капсулу.
        private void SnapToGround()
        {
            Vector3 feet = transform.position + Vector3.down * _feetOffset;
            Vector3 from = feet + Vector3.up * 0.6f;
            var hits = Physics.RaycastAll(from, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                transform.position = new Vector3(transform.position.x, hit.point.y + _feetOffset, transform.position.z);
                return;
            }
        }

        private void LateUpdate()
        {
            if (_nameTag == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            _nameTag.transform.rotation = Quaternion.LookRotation(
                _nameTag.transform.position - cam.transform.position, Vector3.up);
        }

        private IEnumerator VisitRoutine()
        {
            // Встаём в хвост очереди и ждём, пока не подойдём к стойке первыми.
            _shift?.JoinQueue(this);
            _moveRoutine = StartCoroutine(WalkToQueueSlot());

            // Ждём кадрами, а НЕ через «yield return _moveRoutine». Когда очередь
            // сдвигается, MoveToQueueSlot останавливает корутину движения и пускает
            // новую - а остановленная корутина не завершается никогда. Ожидание на
            // её объекте висело бы вечно: такой посетитель навсегда оставался бы
            // «не у стойки» и не отдавал посылку. Это и был баг «коробку берут не у всех»:
            // попадались только те, чьё место в очереди сменилось на ходу.
            while (_shift != null && (!_shift.IsAtCounter(this) || !_slotReached))
            {
                // Подстраховка: стоим на месте, а место уже не наше - идём снова
                if (_slotReached && !AtOwnQueueSlot())
                    RestartWalkToSlot();

                yield return null;
            }

            _atCounter = true;

            if (Kind == NpcKind.Sender)
            {
                while (!_served) yield return null;
            }
            else
            {
                float waited = 0f;
                float missing = 0f;
                while (!_served && (_patience <= 0f || waited < _patience))
                {
                    waited += Time.deltaTime;

                    bool available = _shift == null || _shift.HasParcelFor(RequestedName);
                    missing = available ? 0f : missing + Time.deltaTime;
                    if (missing > _giveUpWhenParcelGone) break;

                    yield return null;
                }

                if (!_served)
                    _shift?.NotifyReceiverGaveUp();
            }

            _atCounter = false;
            _leaving = true;
            _shift?.LeaveQueue(this);   // следующий в очереди сдвигается к стойке

            yield return WalkTo(_exitPoint);

            if (_carriedBox != null) Destroy(_carriedBox.gameObject);
            Destroy(gameObject);
        }

        /// <summary>
        /// Шрифт реплики. Вызывать ДО Begin: компонент вешается на NPC уже в игре,
        /// так что значение из инспектора сюда не попадёт (см. PostOfficeShift).
        /// </summary>
        public void SetFont(TMPro.TMP_FontAsset font) => _nameTagFont = font;

        /// <summary>Очередь сдвинулась - идём на своё новое место.</summary>
        public void MoveToQueueSlot(int index)
        {
            _queueSlot = index;

            // Если NPC уже идёт к стойке или ждёт, но ещё не начал уходить,
            // прерываем текущий путь и перенаправляем в новый слот очереди.
            // VisitRoutine при этом ждёт кадрами, а не эту корутину: остановленная
            // корутина не завершается, и ожидание на ней зависло бы навсегда.
            if (_shift != null && !_leaving && !_atCounter)
                RestartWalkToSlot();
        }

        // Пойти на своё место заново. Флаг «дошёл» обязательно сбрасываем здесь:
        // иначе он остался бы с прошлого места, и посетителя сочли бы стоящим
        // у стойки, пока он ещё шагает к ней с середины очереди
        private void RestartWalkToSlot()
        {
            _slotReached = false;
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(WalkToQueueSlot());
        }

        // Идёт на своё место в очереди. Место может смениться прямо по дороге
        // (кого-то обслужили и очередь сдвинулась), поэтому цель каждый кадр
        // перечитывается у смены, а не запоминается один раз.
        private IEnumerator WalkToQueueSlot()
        {
            if (_shift == null) yield break;

            _queueSlot = _shift.QueueIndexOf(this);
            _slotReached = false;
            SetWalking(true);

            while (_shift != null)
            {
                _queueSlot = _shift.QueueIndexOf(this);
                if (_queueSlot < 0) break;

                Vector3 slot = _shift.QueueSlotPosition(_queueSlot);
                Vector3 to = slot - transform.position;
                to.y = 0f;
                if (to.magnitude <= _stopDistance) break;

                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.position += to.normalized * _walkSpeed * Time.deltaTime;
                SnapToGround();
                yield return null;
            }

            // В очереди все смотрят на стойку, а не в спину переднему
            if (_counterPoint != null)
            {
                Vector3 face = _counterPoint.forward;
                face.y = 0f;
                if (face.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            }

            SetWalking(false);
            Physics.SyncTransforms();
            _slotReached = true;
        }

        // Стоим ли мы на своём нынешнем месте в очереди
        private bool AtOwnQueueSlot()
        {
            if (_shift == null) return true;

            int slot = _shift.QueueIndexOf(this);
            if (slot < 0) return true;

            Vector3 to = _shift.QueueSlotPosition(slot) - transform.position;
            to.y = 0f;
            return to.magnitude <= _stopDistance;
        }

        private IEnumerator WalkTo(Transform target)
        {
            if (target == null) yield break;

            SetWalking(true);

            while (target != null)
            {
                Vector3 to = target.position - transform.position;
                to.y = 0f;
                if (to.magnitude <= _stopDistance) break;

                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.position += to.normalized * _walkSpeed * Time.deltaTime;
                SnapToGround();
                yield return null;
            }

            SetWalking(false);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Посетитель провалился в люк (см. CounterTrapdoor). Падает он скриптом,
        /// а не физикой: Rigidbody у него кинематический (иначе гравитация дерётся
        /// со скриптовой ходьбой), и включать физику на лету - значит получить
        /// посетителя, застрявшего в полу или улетевшего сквозь стену.
        /// </summary>
        public void FallThrough(float depth, float gravity)
        {
            if (_falling) return;
            _falling = true;

            // Все его дела останавливаем: иначе VisitRoutine продолжил бы ходить
            // и каждый кадр возвращал бы его на пол (см. SnapToGround)
            StopAllCoroutines();
            _atCounter = false;
            _leaving = true;
            SetWalking(false);

            // Из очереди убираем сразу, не дожидаясь конца падения: следующий
            // должен шагнуть к стойке, пока этот ещё летит
            _shift?.LeaveQueue(this);

            // Сквозь пол он пролетит только без коллайдеров: иначе зацепится
            // за створки люка или за стойку
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;

            StartCoroutine(FallRoutine(depth, gravity));
        }

        private IEnumerator FallRoutine(float depth, float gravity)
        {
            float startY = transform.position.y;
            float speed = 0f;

            while (startY - transform.position.y < depth)
            {
                speed += gravity * Time.deltaTime;
                transform.position += Vector3.down * speed * Time.deltaTime;
                yield return null;
            }

            // Посылка падает вместе с ним - принять её уже не выйдет
            if (_carriedBox != null) Destroy(_carriedBox.gameObject);
            Destroy(gameObject);
        }

        /// <summary>Уже падает - второй раз в люк не отправить.</summary>
        public bool IsFalling => _falling;

        private void SetWalking(bool walking)
        {
            if (_animator != null && !string.IsNullOrEmpty(_isWalkingParam))
                _animator.SetBool(_isWalkingParam, walking);
        }

        private void OnDestroy()
        {
            _shift?.LeaveQueue(this);
            _shift?.NotifyNpcGone();
        }
    }
}
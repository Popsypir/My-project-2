using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Перенос посылок - ровно как товара в магазине (см. ItemDragController):
    /// курсор зафиксирован по центру экрана, поэтому всё делается прицелом.
    ///
    /// ЛКМ по посылке - берём, пока кнопка зажата - она едет за прицелом на
    /// расстоянии _holdDistance. Колёсико приближает/отдаляет, зажатая ПКМ
    /// крутит саму посылку (камера при этом замирает, см. HeldItemRotationState).
    /// Лапы игрока прилипают к случайным точкам на её поверхности.
    ///
    /// Отпустили ЛКМ: целясь в посетителя - вручаем посылку ему, целясь в
    /// фургон - грузим туда, иначе ставим на поверхность под прицелом
    /// (прилавок, стеллаж, пол).
    ///
    /// Это намеренная отдельная копия логики магазина, а не общий с ним скрипт:
    /// в проекте так уже сделано с движением (SupermarketCharacterMotor), чтобы
    /// правки под почту не ломали магазин и наоборот.
    /// </summary>
    public class BoxCarrier : MonoBehaviour
    {
        [Tooltip("Камера игрока. Если не задана - берётся из детей")]
        [SerializeField] private Camera _camera;
        [Tooltip("Коллайдер игрока. Пока посылка в руках, столкновение с ним игнорируется, " +
                 "иначе игрок упирается в то, что несёт")]
        [SerializeField] private Collider _playerCollider;

        [Header("Прицел")]
        [SerializeField] private float _reachDistance = 4f;
        [Tooltip("Радиус проверки вокруг прицела - чтобы не целиться пиксель в пиксель")]
        [SerializeField] private float _aimRadius = 0.45f;

        [Header("Удержание")]
        [SerializeField] private float _startHoldDistance = 1.5f;
        [SerializeField] private float _minHoldDistance = 0.6f;
        [SerializeField] private float _maxHoldDistance = 2.5f;
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _followSpeed = 15f;
        [SerializeField] private float _rotateSensitivity = 5f;
        [SerializeField] private float _playerClearanceMargin = 0.2f;

        [Header("Лапы")]
        [Tooltip("Кости лап игрока (Armature/Bone.002 и Bone.003). Позиция выставляется в LateUpdate, " +
                 "иначе Animator перезаписывает её своей позой")]
        [SerializeField] private Transform _leftPaw;
        [SerializeField] private Transform _rightPaw;
        [SerializeField] private float _minPawSeparation = 0.05f;
        [SerializeField] private int _pawSeparationRetries = 5;

        [Header("Выкладывание")]
        [SerializeField] private float _placeClearance = 0.02f;
        [Tooltip("С какой высоты посылка аккуратно встаёт на опору под собой. " +
                 "Висит выше - просто выпадает из рук и падает сама")]
        [SerializeField] private float _maxDropHeight = 1.2f;

        private Box _heldBox;
        private Collider _heldCollider;
        private float _holdDistance;
        private Quaternion _rotationOffset;
        private Vector3 _leftPawLocalPos, _rightPawLocalPos;
        private Quaternion _leftPawLocalRot, _rightPawLocalRot;
        private bool _rotationRequested;

        public bool IsCarrying => _heldBox != null;
        public bool IsRotatingBox => _heldBox != null && Input.GetMouseButton(1);

        private void Awake()
        {
            if (_camera == null) _camera = GetComponentInChildren<Camera>(true);
            if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
        }

        private void OnDisable() => SetRotating(false);

        // Заморозка камеры включается и снимается ТОЛЬКО здесь, по факту
        // "крутим мы сейчас посылку или нет". Иначе легко потерять снятие:
        // достаточно отпустить коробку, не отпуская ПКМ - и камера осталась бы
        // замороженной навсегда.
        private void SetRotating(bool on)
        {
            if (on == _rotationRequested) return;

            _rotationRequested = on;
            if (on) HeldItemRotationState.Request();
            else HeldItemRotationState.Release();
        }

        private void Update()
        {
            // Телефон или панель поверх экрана - руки заняты меню. Заморозку камеры
            // при этом обязательно снимаем: иначе она осталась бы висеть после
            // закрытия телефона, если его открыли прямо во время вращения посылки.
            if (_camera == null
                || (PhoneUIController.Instance != null && PhoneUIController.Instance.IsOpen)
                || CursorRequestState.AnyRequested)
            {
                SetRotating(false);
                return;
            }

            if (Input.GetMouseButtonDown(0)) TryGrab();
            else if (Input.GetMouseButtonUp(0) && _heldBox != null) ReleaseHeld();

            // Считается до раннего выхода: иначе, отпустив коробку с зажатой
            // ПКМ, мы бы сюда уже не дошли и камера осталась бы замороженной
            SetRotating(_heldBox != null && Input.GetMouseButton(1));

            if (_heldBox == null) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            _holdDistance = Mathf.Clamp(_holdDistance + scroll * _zoomSpeed, _minHoldDistance, _maxHoldDistance);

            if (Input.GetMouseButton(1))
            {
                float yaw = Input.GetAxis("Mouse X") * _rotateSensitivity;
                float pitch = Input.GetAxis("Mouse Y") * _rotateSensitivity;
                Quaternion delta = Quaternion.AngleAxis(-yaw, Vector3.up) * Quaternion.AngleAxis(pitch, _camera.transform.right);
                _rotationOffset = Quaternion.Inverse(_camera.transform.rotation) * delta * _camera.transform.rotation * _rotationOffset;
            }
        }

        // Посылку ведём через Rigidbody, а не через родителя: так она честно
        // отталкивает другие коробки и не проходит сквозь прилавок.
        private void FixedUpdate()
        {
            if (_heldBox == null) return;

            var rb = _heldBox.Rb;
            if (rb == null) return;

            Vector3 target = ClampAwayFromPlayer(_camera.transform.position + _camera.transform.forward * _holdDistance);
            rb.linearVelocity = (target - rb.position) * _followSpeed;
            rb.MoveRotation(_camera.transform.rotation * _rotationOffset);
            rb.angularVelocity = Vector3.zero;
        }

        // Лапы ставим ПОСЛЕ того, как Animator применил свою позу за кадр
        private void LateUpdate()
        {
            if (_heldBox == null) return;

            ApplyPaw(_leftPaw, _leftPawLocalPos, _leftPawLocalRot);
            ApplyPaw(_rightPaw, _rightPawLocalPos, _rightPawLocalRot);
        }

        private void ApplyPaw(Transform paw, Vector3 localPos, Quaternion localRot)
        {
            if (paw == null || _heldBox == null) return;
            paw.position = _heldBox.transform.TransformPoint(localPos);
            paw.rotation = _heldBox.transform.rotation * localRot;
        }

        private Ray AimRay() => new Ray(_camera.transform.position, _camera.transform.forward);

        /// <summary>
        /// Разбирает всё под прицелом и отдельно возвращает ближайшую ЦЕЛЬ
        /// (посылка, посетитель, фургон) и ближайшую ПОВЕРХНОСТЬ. Нужно именно
        /// так: посетитель стоит за прилавком, и луч задевает край столешницы
        /// на считанные сантиметры раньше самого посетителя - если брать только
        /// первое попадание, через стойку нельзя было бы ни взять, ни отдать.
        /// </summary>
        private void AimScan(Box ignore, out RaycastHit target, out bool hasTarget,
                                          out RaycastHit surface, out bool hasSurface)
        {
            target = default; surface = default;
            hasTarget = false; hasSurface = false;

            var hits = Physics.SphereCastAll(AimRay(), _aimRadius, _reachDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;

                var box = h.collider.GetComponentInParent<Box>();
                if (ignore != null && box == ignore) continue;

                bool isTarget = box != null
                                || h.collider.GetComponentInParent<PostNpc>() != null
                                || h.collider.GetComponentInParent<Van>() != null;

                if (isTarget && !hasTarget) { target = h; hasTarget = true; }
                else if (!isTarget && !hasSurface) { surface = h; hasSurface = true; }

                if (hasTarget && hasSurface) break;
            }
        }

        private void TryGrab()
        {
            if (_heldBox != null) return;
            AimScan(null, out RaycastHit hit, out bool hasTarget, out _, out _);
            if (!hasTarget) return;

            var npc = hit.collider.GetComponentInParent<PostNpc>();
            if (npc != null && npc.OffersParcel)
            {
                Box offered = npc.TakeParcel();
                if (offered != null) StartCarrying(offered, offered.GetComponent<Collider>());
                return;
            }

            var box = hit.collider.GetComponentInParent<Box>();
            if (box != null && !box.IsHeld && !box.IsInVan)
                StartCarrying(box, hit.collider);
        }

        private void StartCarrying(Box box, Collider boxCollider)
        {
            _heldBox = box;
            _heldCollider = boxCollider != null ? boxCollider : box.GetComponent<Collider>();
            _holdDistance = _startHoldDistance;

            box.BeginCarry();
            _rotationOffset = Quaternion.Inverse(_camera.transform.rotation) * box.Rb.rotation;
            ComputePawOffsets(box.transform, _heldCollider);

            if (_playerCollider != null && _heldCollider != null)
                Physics.IgnoreCollision(_heldCollider, _playerCollider, true);
        }

        private void ReleaseHeld()
        {
            Box box = _heldBox;
            Collider col = _heldCollider;
            _heldBox = null;
            _heldCollider = null;

            if (_playerCollider != null && col != null)
                Physics.IgnoreCollision(col, _playerCollider, false);

            if (box == null) return;

            AimScan(box, out RaycastHit target, out bool hasTarget, out RaycastHit surface, out bool hasSurface);

            if (hasTarget)
            {
                var npc = target.collider.GetComponentInParent<PostNpc>();
                if (npc != null && npc.GiveParcel(box)) return;

                var van = target.collider.GetComponentInParent<Van>();
                if (van != null && van.TryAccept(box)) return;
            }

            // Опускаем посылку ровно вниз - на то, над чем она сейчас висит
            // (прилавок, стеллаж, пол). Раньше она ставилась в точку прицела, а
            // прицел достаёт на несколько метров: посмотрел на дальнюю стену -
            // и посылка при отпускании прыгала через всю комнату.
            if (TryFindFloorUnder(box, out Vector3 place))
            {
                box.PlaceAt(place);
                return;
            }

            box.Throw(Vector3.zero);   // под ней пусто - просто выпускаем из рук
        }

        // Ищет опору прямо под посылкой. Саму посылку и игрока пропускаем.
        private bool TryFindFloorUnder(Box box, out Vector3 place)
        {
            place = box.transform.position;

            float halfHeight = 0.25f;
            if (box.GetComponent<Collider>() is BoxCollider bc)
                halfHeight = bc.size.y * box.transform.lossyScale.y * 0.5f;

            var hits = Physics.RaycastAll(box.transform.position, Vector3.down,
                                          halfHeight + _maxDropHeight, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(box.transform)) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;

                place = h.point + Vector3.up * (halfHeight + _placeClearance);
                return true;
            }

            return false;
        }

        private void ComputePawOffsets(Transform target, Collider targetCollider)
        {
            if (targetCollider == null) return;

            Vector3 left = RandomSurfacePoint(targetCollider);
            Vector3 right = RandomSurfacePoint(targetCollider);

            int attempts = 0;
            while ((left - right).sqrMagnitude < _minPawSeparation * _minPawSeparation && attempts < _pawSeparationRetries)
            {
                right = RandomSurfacePoint(targetCollider);
                attempts++;
            }

            Vector3 center = targetCollider.bounds.center;
            StorePawOffset(target, left, center, out _leftPawLocalPos, out _leftPawLocalRot);
            StorePawOffset(target, right, center, out _rightPawLocalPos, out _rightPawLocalRot);
        }

        private void StorePawOffset(Transform item, Vector3 surfacePoint, Vector3 itemCenter,
                                    out Vector3 localPos, out Quaternion localRot)
        {
            Vector3 normal = surfacePoint - itemCenter;
            if (normal.sqrMagnitude < 0.0001f) normal = Random.onUnitSphere;
            normal.Normalize();

            localPos = item.InverseTransformPoint(surfacePoint);
            localRot = Quaternion.Inverse(item.rotation) * Quaternion.LookRotation(normal, Vector3.up);
        }

        private Vector3 RandomSurfacePoint(Collider collider)
        {
            Bounds b = collider.bounds;
            Vector3 far = b.center + Random.onUnitSphere * (b.extents.magnitude * 2f + 1f);
            return collider.ClosestPoint(far);
        }

        // Столкновение с игроком мы намеренно игнорируем, поэтому не даём посылке
        // залезать в его модель вручную.
        private Vector3 ClampAwayFromPlayer(Vector3 targetPoint)
        {
            if (_playerCollider == null) return targetPoint;

            Vector3 axis = _playerCollider.bounds.center;
            Vector3 offset = new Vector3(targetPoint.x - axis.x, 0f, targetPoint.z - axis.z);
            float minDistance = Mathf.Max(_playerCollider.bounds.extents.x, _playerCollider.bounds.extents.z) + _playerClearanceMargin;

            if (offset.magnitude >= minDistance) return targetPoint;

            Vector3 dir = offset.sqrMagnitude < 0.0001f ? _camera.transform.forward : offset.normalized;
            dir.y = 0f;
            return new Vector3(axis.x, targetPoint.y, axis.z) + dir.normalized * minDistance;
        }
    }
}

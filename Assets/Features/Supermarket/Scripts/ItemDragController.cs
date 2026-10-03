using System.Collections;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Игрок всегда может ходить и осматриваться (мышь всегда крутит камеру,
    /// курсор зафиксирован по центру - свободного курсора нет). Поэтому товар
    /// хватается/держится по прицелу в центре экрана, а не по позиции мыши.
    ///
    /// ЛКМ на товаре под прицелом - берём, пока кнопка зажата - товар едет
    /// вдоль направления взгляда камеры на расстоянии _holdDistance. Отпустили
    /// ЛКМ - падает под физикой. Колёсико - приближает/отдаляет.
    ///
    /// Зажатая ПКМ, пока держим товар - вращает сам товар вместо камеры
    /// (FirstPersonCameraController сам замирает в этот момент, проверяя
    /// IsRotatingItem).
    ///
    /// ЛКМ на ВОРЕ (CustomerThief.IsThief) под прицелом - ловит его сразу же
    /// (без удержания/перетаскивания, как товар) - лапы игрока на секунду-
    /// другую "прилипают" к нему точно так же, как к товару, пока идёт
    /// поимка (см. TryGrabThief).
    /// </summary>
    public class ItemDragController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Слой(и), на которых лежат товары (ScannableItem) - чтобы не хватать что попало")]
        [SerializeField] private LayerMask _itemLayer = ~0;

        [Tooltip("Максимальная дистанция луча из центра экрана, на которой ещё можно схватить товар")]
        [SerializeField] private float _grabRaycastDistance = 100f;

        [Tooltip("Насколько резко товар следует за прицелом")]
        [SerializeField] private float _followSpeed = 15f;

        [Header("Приближение колёсиком")]
        [SerializeField] private float _startHoldDistance = 1.8f;
        [SerializeField] private float _minHoldDistance = 0.4f;
        [SerializeField] private float _maxHoldDistance = 2.5f;
        [SerializeField] private float _zoomSpeed = 2f;

        [Header("Вращение (ПКМ, пока держим предмет)")]
        [SerializeField] private float _rotateSensitivity = 5f;

        [Header("Столкновение с игроком")]
        [Tooltip("Коллайдер игрока (обычно CharacterController на корне персонажа). Пока товар в руках, " +
                 "столкновение с ним игнорируется - иначе CharacterController толкает Rigidbody товара, " +
                 "а ItemDragController тут же тащит его обратно к прицелу, и игрок 'упирается' в то, что держит. " +
                 "Если не назначить вручную - при старте попробуем найти сами (GetComponentInParent)")]
        [SerializeField] private Collider _playerCollider;
        [Tooltip("Запас поверх радиуса игрока - товар (а с ним и лапы) никогда не подъезжает к игроку ближе этого расстояния, " +
                 "чтобы не залезать в его собственную модель, например при быстром повороте")]
        [SerializeField] private float _playerClearanceMargin = 0.2f;

        [Header("Лапы игрока (прилипают к случайным точкам на товаре, пока он в руках)")]
        [Tooltip("Перетащи сюда кости/объекты левой и правой лапы из иерархии игрока. " +
                 "Их родитель НЕ меняется - позиция каждый кадр принудительно " +
                 "выставляется в LateUpdate, иначе Animator перезаписывает её своей позой")]
        [SerializeField] private Transform _leftPaw;
        [SerializeField] private Transform _rightPaw;
        [Tooltip("Минимальное расстояние между точками лап на поверхности товара, чтобы они не слипались в одном месте")]
        [SerializeField] private float _minPawSeparation = 0.05f;
        [Tooltip("Сколько раз пробовать перебросить точку правой лапы, если она легла слишком близко к левой")]
        [SerializeField] private int _pawSeparationRetries = 5;

        [Header("Поиск точки на поверхности товара (для лап)")]
        [Tooltip("Во сколько раз дальше половины диагонали товара улетает пробная точка перед ClosestPoint - чтобы гарантированно оказаться снаружи")]
        [SerializeField] private float _surfaceSampleBoundsMultiplier = 2f;
        [Tooltip("Дополнительный запас расстояния поверх диагонали товара")]
        [SerializeField] private float _surfaceSampleMargin = 1f;
        [Tooltip("Ниже этого квадрата длины нормаль до товара считается вырожденной (точка почти в центре) - берём случайное направление вместо неё")]
        [SerializeField] private float _minNormalSqrMagnitude = 0.0001f;

        [Header("Поимка вора")]
        [Tooltip("Сколько секунд лапы держатся на воре после поимки (для красоты - совпадает с временем тряски у CustomerThief)")]
        [SerializeField] private float _thiefGrabPawDuration = 1.5f;

        private ScannableItem _heldItem;
        private Collider _heldItemCollider;
        private float _holdDistance;
        private Quaternion _itemRotationOffset;
        // Куда сейчас "прилипают" лапы - transform товара, пока он в руках,
        // или transform вора, пока идёт поимка. Null - лапы никуда не прилипают.
        private Transform _pawTarget;
        private Vector3 _leftPawLocalPos;
        private Quaternion _leftPawLocalRot;
        private Vector3 _rightPawLocalPos;
        private Quaternion _rightPawLocalRot;

        public bool IsHoldingItem => _heldItem != null;
        public bool IsRotatingItem => _heldItem != null && Input.GetMouseButton(1);

        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_playerCollider == null)
                _playerCollider = GetComponentInParent<Collider>();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                TryGrab();
            else if (Input.GetMouseButtonUp(0))
                Release();

            if (_heldItem == null) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            _holdDistance = Mathf.Clamp(_holdDistance + scroll * _zoomSpeed, _minHoldDistance, _maxHoldDistance);

            // Мышь читаем здесь каждый рендер-кадр, чтобы не терять события мыши между
            // физическими шагами. А применяем поворот к Rigidbody в FixedUpdate через
            // MoveRotation (см. ниже) - так и позиция, и поворот идут из одного и того же
            // физического шага и одинаково сглаживаются Rigidbody Interpolation. Раньше
            // поворот выставлялся напрямую здесь же, в обход физики, и получался рассинхрон
            // с интерполируемой позицией - это и давало остаточную дрожь на лапах.
            if (Input.GetMouseButton(1))
            {
                float yaw = Input.GetAxis("Mouse X") * _rotateSensitivity;
                float pitch = Input.GetAxis("Mouse Y") * _rotateSensitivity;

                // Поворот копится в системе координат камеры, чтобы его не "съедало"
                // собственное вращение товара вместе с камерой ниже.
                Quaternion delta = Quaternion.AngleAxis(-yaw, Vector3.up) * Quaternion.AngleAxis(pitch, _camera.transform.right);
                _itemRotationOffset = Quaternion.Inverse(_camera.transform.rotation) * delta * _camera.transform.rotation * _itemRotationOffset;
            }
        }

        private void FixedUpdate()
        {
            if (_heldItem == null) return;

            Vector3 targetPoint = ClampAwayFromPlayer(GetDragPoint());
            Vector3 toTarget = targetPoint - _heldItem.Rb.position;
            _heldItem.Rb.linearVelocity = toTarget * _followSpeed;

            // Товар держим жёстко относительно камеры - иначе при повороте игрока товар
            // остаётся в прежней мировой ориентации и на экране выглядит так, будто
            // крутится сам по себе. MoveRotation (а не прямое присваивание .rotation) -
            // чтобы поворот участвовал в Rigidbody Interpolation так же, как позиция.
            _heldItem.Rb.MoveRotation(_camera.transform.rotation * _itemRotationOffset);

            // Без этого предмет сам набирает угловую скорость от гравитации/столкновений,
            // пока висит в воздухе на linearVelocity - MoveRotation выше и так задаёт
            // нужный поворот целиком, лишняя угловая скорость только мешает.
            _heldItem.Rb.angularVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            // Ставим лапы ПОСЛЕ того, как Animator применил свою позу за этот кадр -
            // иначе он тут же перезатирает нашу позицию своей анимацией простоя.
            if (_pawTarget == null) return;

            ApplyPaw(_leftPaw, _leftPawLocalPos, _leftPawLocalRot);
            ApplyPaw(_rightPaw, _rightPawLocalPos, _rightPawLocalRot);
        }

        private void ApplyPaw(Transform paw, Vector3 localPos, Quaternion localRot)
        {
            if (paw == null) return;

            paw.position = _pawTarget.TransformPoint(localPos);
            paw.rotation = _pawTarget.rotation * localRot;
        }

        private void TryGrab()
        {
            if (_camera == null) return;

            Ray ray = CenterRay();
            if (!Physics.Raycast(ray, out RaycastHit hit, _grabRaycastDistance, _itemLayer)) return;

            if (TryGrabThief(hit.collider)) return;

            var item = hit.collider.GetComponentInParent<ScannableItem>();
            if (item == null || item.State == ScanState.Bagged) return;

            _heldItem = item;
            _heldItemCollider = hit.collider;
            _pawTarget = item.transform;
            _holdDistance = _startHoldDistance;
            _itemRotationOffset = Quaternion.Inverse(_camera.transform.rotation) * item.Rb.rotation;
            ComputePawOffsets(item.transform, hit.collider);

            if (_playerCollider != null)
                Physics.IgnoreCollision(_heldItemCollider, _playerCollider, true);
        }

        // Ловит вора под прицелом (если он сейчас вор) сразу, без удержания -
        // лапы на _thiefGrabPawDuration секунд "прилипают" к нему точно так же,
        // как к товару, дальше саму поимку (тряска/падение товара/выброс)
        // полностью делает CustomerThief.GetCaught().
        private bool TryGrabThief(Collider hitCollider)
        {
            CustomerThief thief = hitCollider.GetComponentInParent<CustomerThief>();
            if (thief == null || !thief.IsThief) return false;

            Transform thiefTransform = thief.transform;

            _pawTarget = thiefTransform;
            ComputePawOffsets(thiefTransform, hitCollider);

            if (_playerCollider != null)
                Physics.IgnoreCollision(hitCollider, _playerCollider, true);

            thief.GetCaught();
            StartCoroutine(ReleaseThiefPawsAfterDelay(hitCollider, thiefTransform));

            return true;
        }

        private IEnumerator ReleaseThiefPawsAfterDelay(Collider thiefCollider, Transform thiefTransform)
        {
            yield return new WaitForSeconds(_thiefGrabPawDuration);

            if (_playerCollider != null && thiefCollider != null)
                Physics.IgnoreCollision(thiefCollider, _playerCollider, false);

            // Только если за это время не успели схватить что-то ещё (товар) -
            // тогда лапы уже заняты другим, эту привязку трогать не нужно.
            if (_pawTarget == thiefTransform)
                _pawTarget = null;
        }

        private void Release()
        {
            if (_playerCollider != null && _heldItemCollider != null)
                Physics.IgnoreCollision(_heldItemCollider, _playerCollider, false);

            // Отпускаем лапы только если они сейчас держатся именно за товар -
            // если параллельно идёт поимка вора, её отвязку делает своя корутина.
            if (_heldItem != null && _pawTarget == _heldItem.transform)
                _pawTarget = null;

            _heldItem = null;
            _heldItemCollider = null;
        }

        private void ComputePawOffsets(Transform target, Collider targetCollider)
        {
            Vector3 leftPoint = RandomSurfacePoint(targetCollider);
            Vector3 rightPoint = RandomSurfacePoint(targetCollider);

            int attempts = 0;
            while ((leftPoint - rightPoint).sqrMagnitude < _minPawSeparation * _minPawSeparation && attempts < _pawSeparationRetries)
            {
                rightPoint = RandomSurfacePoint(targetCollider);
                attempts++;
            }

            Vector3 center = targetCollider.bounds.center;
            StorePawOffset(target, leftPoint, center, out _leftPawLocalPos, out _leftPawLocalRot);
            StorePawOffset(target, rightPoint, center, out _rightPawLocalPos, out _rightPawLocalRot);
        }

        private void StorePawOffset(Transform item, Vector3 surfacePoint, Vector3 itemCenter, out Vector3 localPos, out Quaternion localRot)
        {
            Vector3 normal = surfacePoint - itemCenter;
            if (normal.sqrMagnitude < _minNormalSqrMagnitude)
                normal = Random.onUnitSphere;
            normal.Normalize();

            Quaternion worldRot = Quaternion.LookRotation(normal, Vector3.up);

            localPos = item.InverseTransformPoint(surfacePoint);
            localRot = Quaternion.Inverse(item.rotation) * worldRot;
        }

        private Vector3 RandomSurfacePoint(Collider collider)
        {
            Bounds b = collider.bounds;
            Vector3 direction = Random.onUnitSphere;
            Vector3 farPoint = b.center + direction * (b.extents.magnitude * _surfaceSampleBoundsMultiplier + _surfaceSampleMargin);
            return collider.ClosestPoint(farPoint);
        }

        private Vector3 GetDragPoint()
        {
            return _camera.transform.position + _camera.transform.forward * _holdDistance;
        }

        // Не даём точке, куда едет товар, подобраться к игроку ближе его собственного
        // радиуса + запаса. Столкновение с игроком мы намеренно игнорируем (см. TryGrab/Release),
        // чтобы CharacterController не толкал товар и не мешал ходьбе - поэтому containment
        // (чтобы товар не влезал в модель игрока) держим тут вручную, а не физикой.
        private Vector3 ClampAwayFromPlayer(Vector3 targetPoint)
        {
            if (_playerCollider == null) return targetPoint;

            Vector3 playerAxis = _playerCollider.bounds.center;
            Vector3 horizontalOffset = new Vector3(targetPoint.x - playerAxis.x, 0f, targetPoint.z - playerAxis.z);

            float minDistance = PlayerHorizontalRadius() + _playerClearanceMargin;
            if (horizontalOffset.magnitude >= minDistance)
                return targetPoint;

            // Направление "наружу" берём из горизонтали взгляда камеры, а НЕ из
            // (targetPoint - playerAxis) - этот вектор около центра игрока (там, где
            // товар чаще всего и оказывается при небольшом holdDistance) почти нулевой,
            // и его направление начинает дёргаться от малейшего дрожания камеры - это
            // и была причина тряски товара и лап.
            Vector3 outward = new Vector3(_camera.transform.forward.x, 0f, _camera.transform.forward.z);
            if (outward.sqrMagnitude < 0.0001f)
                outward = horizontalOffset.sqrMagnitude > 0.0001f ? horizontalOffset : Vector3.forward;
            outward.Normalize();

            Vector3 clampedHorizontal = outward * minDistance;
            return new Vector3(playerAxis.x + clampedHorizontal.x, targetPoint.y, playerAxis.z + clampedHorizontal.z);
        }

        private float PlayerHorizontalRadius()
        {
            if (_playerCollider is CharacterController controller)
            {
                Vector3 scale = controller.transform.lossyScale;
                return controller.radius * Mathf.Max(scale.x, scale.z);
            }

            return Mathf.Max(_playerCollider.bounds.extents.x, _playerCollider.bounds.extents.z);
        }

        private Ray CenterRay()
        {
            return _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
    }
}

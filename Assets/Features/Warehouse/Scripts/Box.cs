using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Коробка склада - можно поднять (см. BoxCarrier), унести и бросить в
    /// фургон (см. Van) или просто уронить на пол. Простая физика через
    /// обычный Rigidbody - пока лежит на полу/несётся игроком, физика
    /// выключена и включается заново при броске, чтобы коробка честно летела
    /// и падала как физический объект.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Box : MonoBehaviour
    {
        [Tooltip("Несколько вариантов текстуры коробки - при спавне выбирается случайно, для разнообразия")]
        [SerializeField] private Material[] _visualVariants;
        [SerializeField] private MeshRenderer _renderer;

        [Header("Бирка")]
        [Tooltip("Шрифт надписи на бирке. Если не задан - берётся шрифт TMP по умолчанию")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;
        [SerializeField] private float _labelSize = 1.6f;
        [Tooltip("Насколько бирка выступает над гранью коробки, чтобы не тонуть в текстуре")]
        [SerializeField] private float _labelOffset = 0.03f;

        [Header("Размер")]
        [Tooltip("Во сколько раз посылка может быть меньше/больше обычной - подбирается случайно при появлении")]
        [SerializeField] private Vector2 _sizeRange = new Vector2(0.75f, 1.45f);

        public bool IsHeld { get; private set; }
        public bool IsInVan { get; private set; }

        /// <summary>Тело посылки - им её и двигает BoxCarrier, как товар в магазине.</summary>
        public Rigidbody Rb => _rb;

        /// <summary>Имя получателя с бирки. Посетитель забирает только свою посылку (см. PostNpc).</summary>
        public string RecipientName { get; private set; }

        private Rigidbody _rb;
        private Collider _collider;
        private Vector3 _worldScale;
        private TMPro.TextMeshPro _labelText;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();

            // Посылки разного размера. Масштаб меняем ДО того, как запомним
            // _worldScale: именно к нему коробка возвращается, когда её кладут.
            if (_sizeRange.y > _sizeRange.x)
            {
                float k = Random.Range(_sizeRange.x, _sizeRange.y);
                transform.localScale *= k;
                _rb.mass *= k * k * k;   // крупная посылка и весит больше
            }
            _worldScale = transform.lossyScale;

            if (_renderer != null && _visualVariants != null && _visualVariants.Length > 0)
                _renderer.sharedMaterial = _visualVariants[Random.Range(0, _visualVariants.Length)];

            if (string.IsNullOrEmpty(RecipientName))
                SetRecipient(ParcelNames.Random());
        }

        /// <summary>Игрок взял посылку в руки - дальше ею двигает физика (см. BoxCarrier).</summary>
        public void BeginCarry()
        {
            IsHeld = true;
            IsInVan = false;
            transform.SetParent(null, true);
            _collider.enabled = true;
            _rb.isKinematic = false;
            _rb.useGravity = false;      // висит на прицеле, а не падает
        }

        /// <summary>Пишет имя на бирке. Бирка создаётся при первом вызове.</summary>
        public void SetRecipient(string name)
        {
            RecipientName = name;

            if (_labelText == null) CreateLabel();
            if (_labelText != null) _labelText.text = name;
        }

        // Наклейка на верхней грани коробки: так имя видно и когда коробка лежит
        // на прилавке, и когда её несут перед собой.
        private void CreateLabel()
        {
            var size = _collider is BoxCollider bc ? bc.size : Vector3.one * 0.5f;

            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, size.y * 0.5f + _labelOffset, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _labelText = go.AddComponent<TMPro.TextMeshPro>();
            if (_labelFont != null) _labelText.font = _labelFont;
            _labelText.color = new Color(0.1f, 0.1f, 0.1f);
            _labelText.alignment = TMPro.TextAlignmentOptions.Center;
            _labelText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _labelText.overflowMode = TMPro.TextOverflowModes.Truncate;

            // Размер шрифта подбирается сам под длину имени и под грань коробки:
            // имена разной длины, а коробки разного размера - фиксированный кегль
            // у кого-нибудь обязательно вылез бы за края.
            _labelText.enableAutoSizing = true;
            _labelText.fontSizeMin = 0.2f;
            _labelText.fontSizeMax = _labelSize;

            var rt = _labelText.rectTransform;
            rt.sizeDelta = new Vector2(size.x * 0.82f, size.z * 0.82f);
        }

        public void PickUp(Transform holdPoint)
        {
            IsHeld = true;
            IsInVan = false;
            _rb.isKinematic = true;
            _collider.enabled = false;
            AttachTo(holdPoint);
        }

        public void Throw(Vector3 force)
        {
            Release();
            _rb.AddForce(force, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Кладёт коробку в указанное место и возвращает ей обычную физику -
        /// так её выкладывает на стойку посетитель (см. PostNpc).
        /// </summary>
        public void PlaceAt(Vector3 position)
        {
            Release(position);
        }

        // Позицию выставляем, пока тело ещё кинематическое, и сразу
        // синхронизируем с физикой: если подвинуть уже "живой" Rigidbody через
        // transform, его коллайдер до следующего кадра физики остаётся на
        // старом месте - коробка видна на новом, а подобрать её там нельзя.
        private void Release(Vector3? position = null)
        {
            IsHeld = false;
            IsInVan = false;
            _rb.isKinematic = true;
            transform.SetParent(null, true);
            transform.localScale = _worldScale;
            if (position.HasValue) transform.position = position.Value;
            _collider.enabled = true;
            Physics.SyncTransforms();

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Аккуратно кладётся в фургон (см. Van.TryAccept) - без физики, чтобы не заваливалась
        public void PlaceInVan(Transform slot)
        {
            IsHeld = false;
            IsInVan = true;
            _rb.isKinematic = true;
            _collider.enabled = false;
            AttachTo(slot);
        }

        // Родитель может быть отмасштабирован (игрок в сценах стоит со scale 0.52) -
        // без компенсации коробка навсегда сжималась бы при первом же подъёме
        private void AttachTo(Transform parent)
        {
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Vector3 p = parent.lossyScale;
            transform.localScale = new Vector3(_worldScale.x / p.x, _worldScale.y / p.y, _worldScale.z / p.z);
        }
    }
}

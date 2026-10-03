using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Слой свободного рисования на доске - обычный RawImage с текстурой,
    /// которую красим по пикселям прямо под курсором, пока зажата мышь и
    /// включён режим рисования или стирания (см. IsDrawingEnabled/IsErasing/
    /// NotesBoardApp.SetMode). Стрелочки, обводки, каракули - что угодно,
    /// одним цветом и толщиной; в режиме стирания то же самое, но прозрачным.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class BoardDrawingLayer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private int _textureWidth = 1600;
        [SerializeField] private int _textureHeight = 1000;
        [SerializeField] private Color _penColor = new Color(0.75f, 0.1f, 0.1f);
        [SerializeField] private int _brushRadius = 4;
        [Tooltip("Ластик крупнее кисти - им проще стирать")]
        [SerializeField] private int _eraserRadius = 10;

        // Переключает NotesBoardApp.SetMode вместе с IsDrawingEnabled - пока true,
        // PaintAt красит прозрачным (стирает) вместо цвета пера
        public bool IsErasing { get; set; }

        // Рисовать разрешено только в режиме Draw - переключает NotesBoardApp.SetMode.
        // Важно: помимо флага ещё и выключаем сам компонент (enabled = false), когда
        // рисование не активно - иначе Unity Input System при поиске, кому отдать
        // перетаскивание, находит IDragHandler прямо на этом объекте (он тут первый
        // под курсором) и отдаёт жест ему, даже если внутри он ничего не делает - и
        // событие никогда не доходит до ScrollRect самой доски, панорамирование
        // молча "не работает". Выключенный компонент Unity в этом поиске пропускает.
        public bool IsDrawingEnabled
        {
            get => enabled;
            set => enabled = value;
        }

        private RawImage _rawImage;
        private Texture2D _texture;
        private RectTransform _rect;
        private Vector2? _lastPixelPos;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _rect = (RectTransform)transform;

            _texture = new Texture2D(_textureWidth, _textureHeight, TextureFormat.RGBA32, false);
            ClearTexturePixels();
            _rawImage.texture = _texture;
        }

        private void ClearTexturePixels()
        {
            var clear = new Color32[_textureWidth * _textureHeight];
            var transparent = new Color32(0, 0, 0, 0);
            for (int i = 0; i < clear.Length; i++) clear[i] = transparent;

            _texture.SetPixels32(clear);
            _texture.Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastPixelPos = ScreenToPixel(eventData);
            PaintAt(_lastPixelPos.Value);
            _texture.Apply();
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 pixel = ScreenToPixel(eventData);

            if (_lastPixelPos.HasValue)
                PaintLine(_lastPixelPos.Value, pixel);
            else
                PaintAt(pixel);

            _lastPixelPos = pixel;
            _texture.Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _lastPixelPos = null;
        }

        // Вешается на 5 быстрых нажатий кнопки "Ластик" (см. NotesBoardApp) -
        // полностью стирает весь рисунок разом
        public void ClearAll()
        {
            ClearTexturePixels();
        }

        // Используется при сохранении/загрузке доски (см. NotesBoardApp.SaveBoard/LoadBoard)
        public byte[] GetPngBytes() => _texture.EncodeToPNG();

        public void LoadFromPngBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            _texture.LoadImage(data);
            _texture.Apply();
        }

        private Vector2 ScreenToPixel(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float u = Mathf.InverseLerp(_rect.rect.xMin, _rect.rect.xMax, local.x);
            float v = Mathf.InverseLerp(_rect.rect.yMin, _rect.rect.yMax, local.y);

            return new Vector2(u * _textureWidth, v * _textureHeight);
        }

        private void PaintAt(Vector2 pixel)
        {
            int cx = Mathf.RoundToInt(pixel.x);
            int cy = Mathf.RoundToInt(pixel.y);

            int radius = IsErasing ? _eraserRadius : _brushRadius;
            Color color = IsErasing ? new Color(0f, 0f, 0f, 0f) : _penColor;

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > radius * radius) continue;

                    int px = cx + x;
                    int py = cy + y;
                    if (px < 0 || px >= _textureWidth || py < 0 || py >= _textureHeight) continue;

                    _texture.SetPixel(px, py, color);
                }
            }
        }

        // Закрашивает по прямой между двумя точками (иначе при быстром движении
        // мыши между кадрами остаются дырки - точки красятся, а линия между ними нет)
        private void PaintLine(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

            for (int i = 0; i <= steps; i++)
                PaintAt(Vector2.Lerp(from, to, i / (float)steps));
        }
    }
}

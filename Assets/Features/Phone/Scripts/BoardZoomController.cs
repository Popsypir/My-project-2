using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone.Apps
{
    /// <summary>
    /// Зум доски колесом мыши - масштабирует сам Content доски (заметки,
    /// рисунок и нитки внутри него отмасштабируются вместе с ним). Вешается
    /// на объект внутри доски, который курсор задевает первым (см. Content) -
    /// иначе колесо просто прокрутило бы ScrollRect, как обычно в Unity UI.
    ///
    /// Зумим не от угла доски, а от точки под курсором - иначе после каждого
    /// зума то, что было под курсором, "убегает" в сторону (особенно заметно
    /// при отдалении - нарисованное просто улетает за край экрана). После
    /// каждого шага зума доску ещё и подтягиваем в границы вьюпорта - иначе
    /// зумом её можно так же увести в пустоту, как и перетаскиванием.
    /// </summary>
    public class BoardZoomController : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private float _zoomSpeed = 0.1f;
        [SerializeField] private float _minZoom = 0.3f;
        [SerializeField] private float _maxZoom = 2.5f;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnScroll(PointerEventData eventData)
        {
            ZoomAroundScreenPoint(eventData.scrollDelta.y * _zoomSpeed, eventData.position);
        }

        private void ZoomAroundScreenPoint(float scaleDelta, Vector2 screenPoint)
        {
            if (_content == null || Mathf.Approximately(scaleDelta, 0f)) return;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPoint, cam, out Vector2 localBefore);

            float newScale = Mathf.Clamp(_content.localScale.x + scaleDelta, _minZoom, _maxZoom);
            _content.localScale = new Vector3(newScale, newScale, 1f);

            // Та же точка на экране после смены масштаба указывает на другой
            // локальный пиксель доски - сдвигаем anchoredPosition ровно настолько,
            // чтобы под курсором остался тот же самый пиксель доски, что и был
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPoint, cam, out Vector2 localAfter);
            _content.anchoredPosition += (localAfter - localBefore) * newScale;

            ClampContentPosition();
        }

        // Content заякорен в верхний левый угол вьюпорта (pivot/anchor (0,1)),
        // поэтому X и Y ведут себя по-разному: увеличение X уводит контент
        // вправо (за левый край), а увеличение Y - вниз (за верхний край).
        // Если контент на текущем зуме меньше вьюпорта по какой-то оси -
        // просто центрируем его по этой оси вместо диапазона.
        // Публичный - чтобы им же мог воспользоваться NotesBoardApp после
        // панорамирования доски средней кнопкой мыши (см. HandleMiddleMousePan).
        public void ClampContentPosition()
        {
            if (_viewport == null) return;

            Vector2 contentSize = _content.rect.size * _content.localScale.x;
            Vector2 viewportSize = _viewport.rect.size;
            Vector2 pos = _content.anchoredPosition;

            pos.x = contentSize.x <= viewportSize.x
                ? (viewportSize.x - contentSize.x) / 2f
                : Mathf.Clamp(pos.x, -(contentSize.x - viewportSize.x), 0f);

            pos.y = contentSize.y <= viewportSize.y
                ? -(viewportSize.y - contentSize.y) / 2f
                : Mathf.Clamp(pos.y, 0f, contentSize.y - viewportSize.y);

            _content.anchoredPosition = pos;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone.Apps
{
    /// <summary>
    /// Вешается на "шапку" заметки (не на всю заметку - иначе перетаскивание
    /// мешало бы печатать в текстовом поле). Тащит саму заметку (её
    /// RectTransform), а не себя. Пока тащим - просим доску временно не
    /// таскаться самой (см. NotesBoardApp.SetBoardScrollEnabled), иначе поехало
    /// бы сразу и то, и то.
    /// </summary>
    public class NoteDragHandle : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _noteRect;
        private NotesBoardApp _board;
        private Canvas _canvas;

        public void Initialize(RectTransform noteRect, NotesBoardApp board)
        {
            _noteRect = noteRect;
            _board = board;
            _canvas = GetComponentInParent<Canvas>();
        }

        // Без этого Unity не находит именно этот компонент как обработчик
        // перетаскивания и поднимается выше по иерархии до ScrollRect доски -
        // в итоге тащилась бы вся доска, а не заметка под курсором.
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _board?.SetBoardScrollEnabled(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_noteRect == null) return;

            // eventData.delta - в экранных пикселях. Доска может быть отмасштабирована
            // и самим Canvas (Scale With Screen Size), и зумом доски (см.
            // BoardZoomController) - делим на оба масштаба, чтобы заметка ехала
            // ровно за курсором на любом уровне приближения.
            float scale = (_canvas != null ? _canvas.scaleFactor : 1f) * (_board != null ? _board.ContentScale : 1f);
            _noteRect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _board?.SetBoardScrollEnabled(true);
        }
    }
}

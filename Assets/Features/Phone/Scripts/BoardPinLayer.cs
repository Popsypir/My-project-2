using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone.Apps
{
    /// <summary>
    /// Ловит клики по пустому месту доски в режиме "Булавка" - чтобы нитку
    /// можно было протянуть не только между заметками, но и в любую точку
    /// доски (см. NotesBoardApp.HandleBoardClickedInPinMode). Висит на том же
    /// слое, что и BoardDrawingLayer, и включается только режимом Pin - друг
    /// другу не мешают, активен всегда только один из них.
    /// </summary>
    public class BoardPinLayer : MonoBehaviour, IPointerClickHandler
    {
        private NotesBoardApp _board;
        private RectTransform _content;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        // Ссылки заводим кодом из NotesBoardApp.Start(), а не через инспектор -
        // ссылки, проставленные вручную через SerializedObject, на этом объекте
        // почему-то не переживали пересборку сцены/скриптов и терялись
        public void Initialize(NotesBoardApp board, RectTransform content)
        {
            _board = board;
            _content = content;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_board == null || _content == null) return;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, eventData.position, cam, out Vector2 local);
            _board.HandleBoardClickedInPinMode(local);
        }
    }
}

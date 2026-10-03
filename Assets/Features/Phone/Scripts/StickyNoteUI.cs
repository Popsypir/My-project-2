using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Одна заметка на доске - фон + текстовое поле (можно писать), таскается
    /// за отдельную "шапку" (см. NoteDragHandle), чтобы не мешать печатать.
    /// Есть булавка (PinAnchor) - к ней цепляются красные нитки (RedStringUI),
    /// когда доска в режиме Pin (см. NotesBoardApp).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StickyNoteUI : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Только эта часть заметки таскает её целиком (например полоска сверху)")]
        [SerializeField] private NoteDragHandle _dragHandle;
        [SerializeField] private TMP_InputField _textField;
        [Tooltip("Точка, к которой цепляются нитки. Если не назначена - используется сам RectTransform заметки")]
        [SerializeField] private RectTransform _pinAnchor;
        [Tooltip("Включается, пока заметка выбрана первой при соединении ниткой (см. NotesBoardApp.HandleNoteClickedInPinMode)")]
        [SerializeField] private Image _pinHighlight;
        [SerializeField] private Button _deleteButton;

        private NotesBoardApp _board;

        public RectTransform PinAnchor => _pinAnchor != null ? _pinAnchor : (RectTransform)transform;

        // Используется при сохранении/загрузке доски - чтобы нитки, привязанные
        // к этой заметке, можно было найти обратно после перезапуска игры
        public string Id { get; private set; }

        public string Text => _textField != null ? _textField.text : string.Empty;

        // id передаём при загрузке сохранённой доски - у новой заметки его нет,
        // генерируем свежий
        public void Initialize(NotesBoardApp board, string id = null)
        {
            _board = board;
            Id = string.IsNullOrEmpty(id) ? System.Guid.NewGuid().ToString() : id;

            if (_dragHandle != null)
                _dragHandle.Initialize((RectTransform)transform, board);

            if (_deleteButton != null)
                _deleteButton.onClick.AddListener(() => _board.RemoveNote(this));

            SetPinHighlighted(false);
        }

        public void SetText(string text)
        {
            if (_textField != null)
                _textField.text = text;
        }

        // Клик по заметке - в режиме Pin выбирает её для соединения ниткой,
        // в остальных режимах ничего не делает (см. NotesBoardApp.CurrentMode)
        public void OnPointerClick(PointerEventData eventData)
        {
            _board?.HandleNoteClickedInPinMode(this);
        }

        public void SetPinHighlighted(bool highlighted)
        {
            if (_pinHighlight != null)
                _pinHighlight.enabled = highlighted;
        }
    }
}

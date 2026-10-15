using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Одна запись в списке событий дня (см. CalendarApp). Показывает текст
    /// и даёт его удалить крестиком.
    /// </summary>
    public class CalendarEventRow : MonoBehaviour
    {
        [Tooltip("Текст записи")]
        [SerializeField] private TMP_Text _label;
        [Tooltip("Крестик, удаляющий запись")]
        [SerializeField] private Button _deleteButton;
        [Tooltip("Необязательно: высота строки подгоняется под длинный текст")]
        [SerializeField] private LayoutElement _layout;
        [Tooltip("Высота строки с короткой записью")]
        [SerializeField] private float _minHeight = 46f;
        [Tooltip("Отступы сверху и снизу, когда запись не влезла в одну строку")]
        [SerializeField] private float _verticalPadding = 14f;

        public CalendarEvent Event { get; private set; }

        private Action<CalendarEvent> _onDelete;

        private void Awake()
        {
            if (_deleteButton != null)
                _deleteButton.onClick.AddListener(() => _onDelete?.Invoke(Event));
        }

        public void Bind(CalendarEvent calendarEvent, Action<CalendarEvent> onDelete)
        {
            Event = calendarEvent;
            _onDelete = onDelete;

            if (_label == null) return;

            _label.text = calendarEvent != null ? calendarEvent.Text : string.Empty;
            FitHeight(_label.text);
        }

        // Длинная запись переносится и строка становится выше, а не мельчит шрифт
        private void FitHeight(string text)
        {
            if (_layout == null || _label == null) return;

            float width = _label.rectTransform.rect.width;
            if (width <= 0f) return;

            float textHeight = _label.GetPreferredValues(text, width, 0f).y;
            _layout.preferredHeight = Mathf.Max(_minHeight, textHeight + _verticalPadding);
        }
    }
}

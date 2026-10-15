using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Одна клетка календаря - число месяца (см. CalendarApp). Сама ничего не
    /// считает: ей говорят, какое число показать и чем его выделить.
    ///
    /// По клетке можно щёлкнуть - откроется список событий этого дня.
    ///
    /// Все поля кроме текста необязательные: не назначил обводку - просто не
    /// будет обводки, остальное работает.
    /// </summary>
    public class CalendarDayCell : MonoBehaviour
    {
        [Tooltip("Само число")]
        [SerializeField] private TMP_Text _label;
        [Tooltip("Обводка сегодняшнего дня - кружок поверх числа")]
        [SerializeField] private GameObject _todayRing;
        [Tooltip("Точка под числом: в этот день что-то записано")]
        [SerializeField] private GameObject _eventMark;
        [Tooltip("Необязательно: вторая точка для уже прожитых дней")]
        [SerializeField] private GameObject _pastMark;
        [Tooltip("Подложка клетки - подсвечивается, когда день выбран")]
        [SerializeField] private Graphic _background;
        [Tooltip("Нажатие по клетке. Не назначишь - дни просто не будут открываться")]
        [SerializeField] private Button _button;

        [Header("Цвета числа")]
        [Tooltip("Обычный день текущего месяца")]
        [SerializeField] private Color _normalColor = new Color(1f, 0.95f, 0.86f, 1f);
        [Tooltip("Числа соседних месяцев - бледные, чтобы сетка не рвалась")]
        [SerializeField] private Color _otherMonthColor = new Color(1f, 0.95f, 0.86f, 0.25f);
        [Tooltip("Уже прожитый день - приглушённый")]
        [SerializeField] private Color _pastColor = new Color(1f, 0.95f, 0.86f, 0.5f);
        [Tooltip("Сегодняшнее число")]
        [SerializeField] private Color _todayColor = Color.white;

        [Header("Цвет подложки")]
        [Tooltip("Подложка обычной клетки. Прозрачная - значит её не видно")]
        [SerializeField] private Color _backgroundNormal = new Color(1f, 1f, 1f, 0f);
        [Tooltip("Подложка выбранной клетки - по ней видно, чей день сейчас открыт")]
        [SerializeField] private Color _backgroundSelected = new Color(1f, 1f, 1f, 0.16f);

        /// <summary>Дата, которую показывает клетка.</summary>
        public DateTime Date { get; private set; }

        public int DayNumber => Date.Day;
        public bool IsToday { get; private set; }
        public bool IsSelected { get; private set; }
        public bool HasEvents { get; private set; }

        private Action<CalendarDayCell> _onClick;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClick);
        }

        private void HandleClick() => _onClick?.Invoke(this);

        public void Bind(DateTime date, bool inShownMonth, bool isToday, bool isPast, bool hasEvents,
                         Action<CalendarDayCell> onClick)
        {
            Date = date;
            IsToday = isToday;
            HasEvents = hasEvents;
            _onClick = onClick;

            if (_label != null)
            {
                _label.text = date.Day.ToString();

                // Порядок важен: сегодняшний день остаётся ярким, даже если
                // он же попал в "прошедшие" или в соседний месяц
                _label.color = isToday ? _todayColor
                             : !inShownMonth ? _otherMonthColor
                             : isPast ? _pastColor
                             : _normalColor;
            }

            if (_todayRing != null) _todayRing.SetActive(isToday);
            if (_eventMark != null) _eventMark.SetActive(hasEvents);
            if (_pastMark != null) _pastMark.SetActive(isPast && inShownMonth && !isToday);

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_background != null)
                _background.color = selected ? _backgroundSelected : _backgroundNormal;
        }
    }
}

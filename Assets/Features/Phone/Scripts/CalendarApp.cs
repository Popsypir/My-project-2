using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Календарь": сетка чисел месяца, сегодняшний день обведён.
    /// По клетке можно щёлкнуть и записать на этот день событие - такие дни
    /// помечаются точкой, а сами записи живут между запусками игры
    /// (см. CalendarEvents).
    ///
    /// Сегодня - это настоящая сегодняшняя дата с часов компьютера, как и
    /// время на экране телефона (см. PhoneClockDisplay). Если понадобится
    /// привязать календарь к игровым суткам (счётчик дней растёт, когда игрок
    /// спит), сними галочку "Брать настоящую дату" - тогда день 1 будет той
    /// датой, что задана ниже.
    ///
    /// Клетки создаются копированием образца _cellTemplate - руками 42 штуки
    /// расставлять не нужно (см. CalendarDayCell).
    /// </summary>
    public class CalendarApp : PhoneAppBase
    {
        [Header("Сетка")]
        [Tooltip("Куда складываются клетки. На нём Grid Layout Group в 7 колонок")]
        [SerializeField] private RectTransform _grid;
        [Tooltip("Выключенный образец клетки - с него делаются копии")]
        [SerializeField] private CalendarDayCell _cellTemplate;

        [Header("Шапка")]
        [Tooltip("Название месяца и год, например \"Сентябрь 2026\"")]
        [SerializeField] private TMP_Text _monthTitle;
        [Tooltip("Необязательно: строка вида \"День 7\" - какой это день игры")]
        [SerializeField] private TMP_Text _gameDayText;
        [Tooltip("Необязательно: подписи дней недели, слева направо. Заполню сам")]
        [SerializeField] private TMP_Text[] _weekdayLabels;

        [Header("Листание месяцев (необязательно)")]
        [Tooltip("Кнопка «предыдущий месяц»")]
        [SerializeField] private Button _prevMonthButton;
        [Tooltip("Кнопка «следующий месяц»")]
        [SerializeField] private Button _nextMonthButton;
        [Tooltip("Возвращает к месяцу, в котором сегодня")]
        [SerializeField] private Button _todayButton;

        [Header("События дня")]
        [Tooltip("Панель поверх сетки - открывается по клику на число")]
        [SerializeField] private GameObject _dayPanel;
        [Tooltip("Заголовок панели, например \"17 сентября, четверг\"")]
        [SerializeField] private TMP_Text _dayTitle;
        [Tooltip("Куда складываются строки событий")]
        [SerializeField] private RectTransform _eventList;
        [Tooltip("Выключенный образец строки события")]
        [SerializeField] private CalendarEventRow _eventRowTemplate;
        [Tooltip("Поле ввода новой записи")]
        [SerializeField] private TMP_InputField _eventInput;
        [Tooltip("Кнопка \"+\" - добавляет то, что набрано в поле")]
        [SerializeField] private Button _addEventButton;
        [Tooltip("Кнопка закрытия панели дня")]
        [SerializeField] private Button _closeDayButton;
        [Tooltip("Подсказка, когда на этот день ничего не записано")]
        [SerializeField] private GameObject _noEventsHint;

        [Header("Какую дату считать сегодняшней")]
        [Tooltip("Включено - настоящая дата компьютера, как часы в телефоне. " +
                 "Выключено - дата считается от игрового дня (см. поля ниже)")]
        [SerializeField] private bool _useRealDate = true;
        [Tooltip("Какой датой считать день 1 - только когда галочка выше снята")]
        [SerializeField] private int _startYear = 2026;
        [SerializeField, Range(1, 12)] private int _startMonth = 9;
        [SerializeField, Range(1, 31)] private int _startDayOfMonth = 1;

        [Header("Подписи")]
        [Tooltip("Названия месяцев для заголовка: Сентябрь 2026")]
        [SerializeField]
        private string[] _monthNames =
        {
            "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
            "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        [Tooltip("Короткие подписи дней недели над сеткой")]
        [SerializeField]
        private string[] _weekdayNames = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

        [Tooltip("Месяц в заголовке дня: \"17 сентября\"")]
        [SerializeField]
        private string[] _monthNamesGenitive =
        {
            "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря"
        };

        [Tooltip("Полные названия дней недели для заголовка дня: 17 сентября, четверг")]
        [SerializeField]
        private string[] _weekdayFullNames =
        {
            "понедельник", "вторник", "среда", "четверг", "пятница", "суббота", "воскресенье"
        };

        [Tooltip("Как подписывается номер игрового дня. {0} заменяется на само число")]
        [SerializeField] private string _gameDayFormat = "День {0}";

        // Шесть строк по семь дней - в любой месяц влезает с запасом,
        // и сетка не прыгает по высоте от месяца к месяцу
        private const int WeeksShown = 6;
        private const int CellCount = WeeksShown * 7;

        private readonly List<CalendarDayCell> _cells = new();
        private readonly List<CalendarEventRow> _eventRows = new();
        private GameDayProviderBase _dayProvider;
        private CalendarDayCell _selectedCell;

        /// <summary>Сегодняшняя дата - настоящая или посчитанная от игрового дня.</summary>
        public DateTime Today => _useRealDate
            ? DateTime.Now.Date
            : StartDate.AddDays(Mathf.Max(1, CurrentGameDay) - 1);

        /// <summary>Месяц, который сейчас открыт (первое число этого месяца).</summary>
        public DateTime ShownMonth { get; private set; }

        /// <summary>Какой день открыт в панели событий. Панель закрыта - null.</summary>
        public DateTime? SelectedDate { get; private set; }

        public int CurrentGameDay => _dayProvider != null ? _dayProvider.CurrentDay : 1;

        private DateTime StartDate
        {
            get
            {
                // Инспектор позволяет выставить, например, 31 февраля - подрезаем
                // число по длине месяца, иначе DateTime бросит исключение
                int year = Mathf.Clamp(_startYear, 1, 9999);
                int month = Mathf.Clamp(_startMonth, 1, 12);
                int day = Mathf.Clamp(_startDayOfMonth, 1, DateTime.DaysInMonth(year, month));
                return new DateTime(year, month, day);
            }
        }

        private void Awake()
        {
            if (_cellTemplate != null) _cellTemplate.gameObject.SetActive(false);
            if (_eventRowTemplate != null) _eventRowTemplate.gameObject.SetActive(false);

            if (_prevMonthButton != null) _prevMonthButton.onClick.AddListener(() => ShiftMonth(-1));
            if (_nextMonthButton != null) _nextMonthButton.onClick.AddListener(() => ShiftMonth(1));
            if (_todayButton != null) _todayButton.onClick.AddListener(GoToToday);
            if (_addEventButton != null) _addEventButton.onClick.AddListener(AddEventFromInput);
            if (_closeDayButton != null) _closeDayButton.onClick.AddListener(CloseDay);

            // Enter в поле ввода добавляет запись - не тянуться мышкой к "+"
            if (_eventInput != null) _eventInput.onSubmit.AddListener(_ => AddEventFromInput());

            FillWeekdayLabels();
            CloseDay();
        }

        public override void OnOpen()
        {
            base.OnOpen();

            // Поставщика дня ищем при каждом открытии: телефон живёт на всех
            // локациях, а DayManager переезжает между сценами сам
            FindDayProvider();
            CalendarEvents.OnChanged += HandleEventsChanged;

            CloseDay();

            // Открывается всегда на сегодняшнем месяце - как на настоящем телефоне
            GoToToday();
        }

        public override void OnClose()
        {
            CalendarEvents.OnChanged -= HandleEventsChanged;
            if (_dayProvider != null) _dayProvider.OnNewDay -= HandleNewDay;

            // CloseDay сам допишет набранное, но не добавленное
            CloseDay();

            base.OnClose();
        }

        private void FindDayProvider()
        {
            if (_dayProvider != null) _dayProvider.OnNewDay -= HandleNewDay;

            // Сначала спрашиваем DayManager - это настоящий счётчик дней, он
            // растёт, когда игрок спит, и переезжает между сценами сам.
            // В квартире рядом с ним лежит ещё и тестовая заглушка
            // SimpleGameDayProvider: если брать первого попавшегося, календарь
            // может прицепиться к ней, а она день не меняет никогда.
            _dayProvider = DayManager.Instance != null
                ? DayManager.Instance
                : FindAnyObjectByType<GameDayProviderBase>(FindObjectsInactive.Include);

            if (_dayProvider != null) _dayProvider.OnNewDay += HandleNewDay;
        }

        // Игрок лёг спать прямо с открытым календарём - сегодняшний день уехал
        private void HandleNewDay() => GoToToday();

        // Запись добавили или удалили - обновляем и точки в сетке, и список
        private void HandleEventsChanged()
        {
            Rebuild();
            if (SelectedDate.HasValue) RefreshEventList();
        }

        public void GoToToday()
        {
            DateTime today = Today;
            ShownMonth = new DateTime(today.Year, today.Month, 1);
            Rebuild();
        }

        public void ShiftMonth(int delta)
        {
            ShownMonth = ShownMonth.AddMonths(delta);
            Rebuild();
        }

        private void Rebuild()
        {
            EnsureCells();

            DateTime today = Today;

            // Неделя начинается с понедельника: у DayOfWeek воскресенье = 0,
            // поэтому сдвигаем, иначе месяц встал бы не под теми подписями
            int firstColumn = ((int)ShownMonth.DayOfWeek + 6) % 7;
            DateTime firstCellDate = ShownMonth.AddDays(-firstColumn);

            for (int i = 0; i < _cells.Count; i++)
            {
                DateTime date = firstCellDate.AddDays(i);

                _cells[i].Bind(
                    date,
                    inShownMonth: date.Month == ShownMonth.Month && date.Year == ShownMonth.Year,
                    isToday: date == today,
                    isPast: date < today,
                    hasEvents: CalendarEvents.HasOn(date),
                    onClick: HandleDayClicked);
            }

            // Выбранный день мог оказаться в другом месяце - подсветку
            // восстанавливаем после перестроения, иначе она потеряется
            RestoreSelection();

            if (_monthTitle != null)
                _monthTitle.text = $"{MonthName(ShownMonth.Month)} {ShownMonth.Year}";

            if (_gameDayText != null)
                _gameDayText.text = string.Format(_gameDayFormat, CurrentGameDay);

            // На сегодняшнем месяце возвращаться уже некуда
            if (_todayButton != null)
                _todayButton.interactable = ShownMonth.Month != today.Month || ShownMonth.Year != today.Year;
        }

        private void EnsureCells()
        {
            if (_grid == null || _cellTemplate == null || _cells.Count == CellCount) return;

            foreach (CalendarDayCell cell in _cells)
                if (cell != null) Destroy(cell.gameObject);
            _cells.Clear();

            for (int i = 0; i < CellCount; i++)
            {
                CalendarDayCell cell = Instantiate(_cellTemplate, _grid);
                cell.gameObject.SetActive(true);
                cell.name = $"Day_{i:00}";
                _cells.Add(cell);
            }
        }

        // ---- события дня ----

        private void HandleDayClicked(CalendarDayCell cell)
        {
            if (cell == null) return;
            OpenDay(cell.Date);
        }

        public void OpenDay(DateTime date)
        {
            // Открыли другой день, не дописав предыдущую запись - сохраняем её
            CommitPendingInput();

            SelectedDate = date;
            RestoreSelection();

            if (_dayPanel != null) _dayPanel.SetActive(true);
            if (_dayTitle != null) _dayTitle.text = DayTitle(date);
            if (_eventInput != null) _eventInput.text = string.Empty;

            RefreshEventList();
        }

        public void CloseDay()
        {
            // Набрал запись и закрыл день крестиком, не нажав "+" - она не
            // должна пропасть. Строго до обнуления SelectedDate: иначе
            // записывать будет уже некуда
            CommitPendingInput();

            SelectedDate = null;
            RestoreSelection();

            if (_dayPanel != null) _dayPanel.SetActive(false);
            ClearEventRows();
        }

        public void AddEventFromInput()
        {
            if (_eventInput == null || !SelectedDate.HasValue) return;

            string text = _eventInput.text;
            _eventInput.text = string.Empty;

            CalendarEvents.Add(SelectedDate.Value, text);

            // Поле остаётся активным - можно писать следующую запись подряд
            _eventInput.ActivateInputField();
        }

        // Набрали текст и закрыли день, не нажав "+" - запись не должна пропасть
        private void CommitPendingInput()
        {
            if (_eventInput == null || !SelectedDate.HasValue) return;
            if (string.IsNullOrWhiteSpace(_eventInput.text)) return;

            CalendarEvents.Add(SelectedDate.Value, _eventInput.text);
            _eventInput.text = string.Empty;
        }

        private void RefreshEventList()
        {
            ClearEventRows();

            if (!SelectedDate.HasValue || _eventList == null || _eventRowTemplate == null) return;

            List<CalendarEvent> events = CalendarEvents.On(SelectedDate.Value);

            foreach (CalendarEvent e in events)
            {
                CalendarEventRow row = Instantiate(_eventRowTemplate, _eventList);
                row.gameObject.SetActive(true);
                row.Bind(e, RemoveEvent);
                _eventRows.Add(row);
            }

            if (_noEventsHint != null) _noEventsHint.SetActive(events.Count == 0);
        }

        private void RemoveEvent(CalendarEvent e) => CalendarEvents.Remove(e);

        private void ClearEventRows()
        {
            foreach (CalendarEventRow row in _eventRows)
                if (row != null)
                {
                    // Сначала отцепляем, чтобы раскладка сразу перестала учитывать
                    // строку: сам объект удалится только в конце кадра
                    row.transform.SetParent(null, false);
                    Destroy(row.gameObject);
                }

            _eventRows.Clear();
            if (_noEventsHint != null) _noEventsHint.SetActive(false);
        }

        private void RestoreSelection()
        {
            _selectedCell = null;

            foreach (CalendarDayCell cell in _cells)
            {
                bool selected = SelectedDate.HasValue && cell.Date == SelectedDate.Value;
                cell.SetSelected(selected);
                if (selected) _selectedCell = cell;
            }
        }

        // ---- подписи ----

        private void FillWeekdayLabels()
        {
            if (_weekdayLabels == null) return;

            for (int i = 0; i < _weekdayLabels.Length; i++)
                if (_weekdayLabels[i] != null && _weekdayNames != null && i < _weekdayNames.Length)
                    _weekdayLabels[i].text = _weekdayNames[i];
        }

        private string MonthName(int month)
        {
            int index = Mathf.Clamp(month, 1, 12) - 1;
            return _monthNames != null && index < _monthNames.Length ? _monthNames[index] : month.ToString();
        }

        private string DayTitle(DateTime date)
        {
            int m = date.Month - 1;
            string month = _monthNamesGenitive != null && m < _monthNamesGenitive.Length
                ? _monthNamesGenitive[m]
                : date.Month.ToString();

            int w = ((int)date.DayOfWeek + 6) % 7;
            string weekday = _weekdayFullNames != null && w < _weekdayFullNames.Length
                ? _weekdayFullNames[w]
                : string.Empty;

            return string.IsNullOrEmpty(weekday)
                ? $"{date.Day} {month}"
                : $"{date.Day} {month}, {weekday}";
        }
    }
}

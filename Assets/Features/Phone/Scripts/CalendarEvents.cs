using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GamePhone.Apps
{
    [Serializable]
    public class CalendarEvent
    {
        public string Id;
        /// <summary>Дата в виде "2026-09-17" - так её переживает JsonUtility, DateTime он не умеет.</summary>
        public string Date;
        public string Text;

        public DateTime When => CalendarEvents.ParseDate(Date);
    }

    /// <summary>
    /// События, которые игрок сам записал в календарь (см. CalendarApp).
    /// Хранятся на диске рядом с остальными сохранениями телефона и живут
    /// между запусками игры.
    ///
    /// Статическое, как BankAccount: телефон есть на всех локациях, и события
    /// должны быть одни и те же, какой бы экземпляр телефона их ни спросил.
    /// </summary>
    public static class CalendarEvents
    {
        [Serializable]
        private class SaveData
        {
            public List<CalendarEvent> Events = new();
        }

        private const string FileName = "calendar_events.json";

        private static List<CalendarEvent> _events;

        /// <summary>Список изменился - перерисовать календарь.</summary>
        public static event Action OnChanged;

        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static IReadOnlyList<CalendarEvent> All
        {
            get { EnsureLoaded(); return _events; }
        }

        public static string Key(DateTime date) => date.ToString("yyyy-MM-dd");

        public static DateTime ParseDate(string key) =>
            DateTime.TryParseExact(key, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime d)
                ? d
                : DateTime.MinValue;

        /// <summary>Все события этого дня, в порядке добавления.</summary>
        public static List<CalendarEvent> On(DateTime date)
        {
            EnsureLoaded();
            string key = Key(date);
            var result = new List<CalendarEvent>();

            foreach (CalendarEvent e in _events)
                if (e != null && e.Date == key)
                    result.Add(e);

            return result;
        }

        /// <summary>Есть ли в этот день хоть одно событие - по этому в сетке ставится точка.</summary>
        public static bool HasOn(DateTime date)
        {
            EnsureLoaded();
            string key = Key(date);

            foreach (CalendarEvent e in _events)
                if (e != null && e.Date == key)
                    return true;

            return false;
        }

        public static int CountOn(DateTime date) => On(date).Count;

        public static CalendarEvent Add(DateTime date, string text)
        {
            EnsureLoaded();

            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return null;   // пустые записи не заводим

            var e = new CalendarEvent
            {
                Id = Guid.NewGuid().ToString(),
                Date = Key(date),
                Text = text
            };

            _events.Add(e);
            Save();
            return e;
        }

        public static void SetText(CalendarEvent e, string text)
        {
            if (e == null) return;

            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) { Remove(e); return; }

            e.Text = text;
            Save();
        }

        public static void Remove(CalendarEvent e)
        {
            EnsureLoaded();
            if (e != null && _events.Remove(e)) Save();
        }

        /// <summary>Стереть всё - пригодится для кнопки "сбросить прогресс" и тестам.</summary>
        public static void Clear()
        {
            EnsureLoaded();
            _events.Clear();
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_events != null) return;
            _events = new List<CalendarEvent>();

            try
            {
                if (!File.Exists(SavePath)) return;

                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data?.Events == null) return;

                foreach (CalendarEvent e in data.Events)
                    if (e != null && !string.IsNullOrWhiteSpace(e.Text) && !string.IsNullOrEmpty(e.Date))
                        _events.Add(e);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Календарь] Не удалось прочитать события: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveData { Events = _events }, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Календарь] Не удалось сохранить события: {ex.Message}");
            }

            OnChanged?.Invoke();
        }

        // Между запусками Play Mode статика в редакторе не обнуляется сама -
        // без этого список остался бы от прошлого запуска, а файл мог измениться
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _events = null;
            OnChanged = null;
        }
    }
}

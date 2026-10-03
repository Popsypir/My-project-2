using System;

namespace GamePhone
{
    /// <summary>
    /// Один контакт, добавленный ИГРОКОМ вручную через приложение "Контакты".
    /// Не путать с PhoneNumberEntry - тот описывает номера, зашитые в игру
    /// дизайнером (с точкой телепортации), этот - просто "имя + номер",
    /// которые игрок сам запомнил и вписал.
    /// </summary>
    [Serializable]
    public class PhoneContact
    {
        public string Name;
        public string Number;
    }
}

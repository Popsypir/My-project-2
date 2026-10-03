namespace GamePhone
{
    public enum TimeOfDay
    {
        Morning,
        Day,
        Evening,
        Night
    }

    /// <summary>
    /// Заготовка на будущее - чтобы диалоги (например при звонке на работу,
    /// см. CallApp/PhoneNumberEntry) могли зависеть от реального времени суток
    /// на компьютере игрока. Пока нигде не используется - просто готовый
    /// способ узнать "сейчас утро/день/вечер/ночь", когда понадобится
    /// прикрутить разные звонки/реплики под разное время суток.
    ///
    /// Пример будущего использования: в PhoneNumberEntry вместо одного
    /// AudioClip CallAudio сделать массив на каждое TimeOfDay, и в CallApp
    /// выбирать нужный через RealTimeOfDay.Current.
    /// </summary>
    public static class RealTimeOfDay
    {
        public static TimeOfDay Current
        {
            get
            {
                int hour = System.DateTime.Now.Hour;

                if (hour >= 6 && hour < 12) return TimeOfDay.Morning;
                if (hour >= 12 && hour < 18) return TimeOfDay.Day;
                if (hour >= 18 && hour < 23) return TimeOfDay.Evening;
                return TimeOfDay.Night;
            }
        }
    }
}

namespace GamePhone
{
    /// <summary>
    /// Некоторые экраны поверх игры (например, оверлей Escape-паузы мини-игры в
    /// PongApp) должны держать курсор свободным, даже когда сам телефон уже
    /// закрыт (PhoneCursorController иначе снова заблокирует его - он ничего
    /// не знает про такие оверлеи). Любой такой экран регистрирует себя здесь
    /// на время своего показа - Request() при показе, Release() при скрытии
    /// (строго парой, иначе счётчик не сойдётся).
    /// </summary>
    public static class CursorRequestState
    {
        private static int _count;

        public static bool AnyRequested => _count > 0;

        public static void Request()
        {
            _count++;
        }

        public static void Release()
        {
            if (_count > 0)
                _count--;
        }
    }
}

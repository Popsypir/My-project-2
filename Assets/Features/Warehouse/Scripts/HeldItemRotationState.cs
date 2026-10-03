namespace GamePhone.Warehouse
{
    /// <summary>
    /// Пока игрок крутит посылку в руках зажатой ПКМ, камера должна замереть -
    /// иначе одна и та же мышь одновременно вертит и предмет, и обзор.
    ///
    /// В магазине для этого FirstPersonCameraController держит прямую ссылку на
    /// ItemDragController. На почте скрипт другой, поэтому связь сделана через
    /// общий счётчик: Request и Release строго парные, иначе камера залипнет
    /// неподвижной навсегда.
    /// </summary>
    public static class HeldItemRotationState
    {
        private static int _requests;

        public static bool AnyRequested => _requests > 0;

        public static void Request() => _requests++;

        public static void Release()
        {
            if (_requests > 0) _requests--;
        }

        /// <summary>
        /// Сбрасывает заморозку начисто. Вызывается при входе в Play Mode: статика
        /// переживает остановку игры, и зависшее значение заблокировало бы камеру
        /// сразу при следующем запуске.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetOnPlay() => _requests = 0;
    }
}

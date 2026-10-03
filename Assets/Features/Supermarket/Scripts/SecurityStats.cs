using System;

namespace GamePhone.Shop
{
    /// <summary>
    /// Статистика работы охранника за смену - сколько воров поймали, сколько
    /// сбежало, сколько товаров вернули на полки, сколько потеряно безвозвратно
    /// (унесено сбежавшим вором или упало в яму). Простой статический класс,
    /// как GameSettings/ContactsStorage - живёт в памяти всю сессию игры,
    /// ничего никуда не сохраняет.
    /// </summary>
    public static class SecurityStats
    {
        public static int ThievesCaught { get; private set; }
        public static int ThievesEscaped { get; private set; }
        public static int ItemsReturned { get; private set; }
        public static int ItemsLost { get; private set; }

        public static event Action OnChanged;

        // Вызывается CustomerThief.GetCaught()
        public static void RegisterThiefCaught()
        {
            ThievesCaught++;
            OnChanged?.Invoke();
        }

        // Вызывается CustomerThief.NotifyEscaped()
        public static void RegisterThiefEscaped()
        {
            ThievesEscaped++;
            OnChanged?.Invoke();
        }

        // Вызывается ShopShelfStock, когда охранник донёс товар до полки
        public static void RegisterItemReturned()
        {
            ItemsReturned++;
            OnChanged?.Invoke();
        }

        // Вызывается CustomerThief.NotifyEscaped() (унёс с собой) и JobFailZone
        // (упал в яму, потерян безвозвратно)
        public static void RegisterItemLost()
        {
            ItemsLost++;
            OnChanged?.Invoke();
        }

        // На случай если понадобится сбрасывать счётчик в начале новой смены
        public static void ResetAll()
        {
            ThievesCaught = 0;
            ThievesEscaped = 0;
            ItemsReturned = 0;
            ItemsLost = 0;
            OnChanged?.Invoke();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// "Пул" всех товаров, которые могут попасться покупателю. Создаётся как ассет:
    /// правый клик в Project -> Create -> Game Phone -> Shop Item Database.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Phone/Shop Item Database", fileName = "ShopItemDatabase")]
    public class ShopItemDatabase : ScriptableObject
    {
        [Tooltip("Все возможные товары. Для каждого покупателя случайно выбирается несколько из них")]
        public List<ShopItemData> AllItems = new();

        [Header("Сколько товаров у одного покупателя")]
        [SerializeField] private int _minItemsPerCustomer = 2;
        [SerializeField] private int _maxItemsPerCustomer = 6;

        public int MinItemsPerCustomer => _minItemsPerCustomer;
        public int MaxItemsPerCustomer => _maxItemsPerCustomer;
    }
}

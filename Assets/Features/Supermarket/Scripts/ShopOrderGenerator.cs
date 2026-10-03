using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Генерирует случайный список товаров для одного покупателя из базы товаров.
    /// </summary>
    public static class ShopOrderGenerator
    {
        public static List<ShopItemData> GenerateOrder(ShopItemDatabase database)
        {
            var order = new List<ShopItemData>();

            if (database == null || database.AllItems.Count == 0)
                return order;

            int count = Random.Range(database.MinItemsPerCustomer, database.MaxItemsPerCustomer + 1);

            for (int i = 0; i < count; i++)
                order.Add(database.AllItems[Random.Range(0, database.AllItems.Count)]);

            return order;
        }
    }
}

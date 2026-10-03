using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Один вид товара в магазине. Создаётся как ассет:
    /// правый клик в Project -> Create -> Game Phone -> Shop Item.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Phone/Shop Item", fileName = "NewShopItem")]
    public class ShopItemData : ScriptableObject
    {
        [Tooltip("Просто для себя - как называется товар")]
        public string ItemName;

        [Tooltip("Цена товара")]
        public float Price;

        [Tooltip("3D-модель товара. На префабе должны быть Rigidbody, Collider и компонент ScannableItem")]
        public GameObject ItemPrefab;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Ставится на точку полки (см. CustomerShopper._shelfPoints). В
    /// _displayItems перетаскиваются уже существующие на сцене объекты товара,
    /// которые на этой полке лежат (banки/коробки и т.п. - обычно их там сразу
    /// много). Чтобы не тащить каждую по отдельности - в Hierarchy выдели ВСЕ
    /// нужные объекты разом (первый - клик, остальные - Shift/Ctrl+клик) и
    /// перетащи всё выделение одним движением прямо на поле Display Items в
    /// инспекторе - Unity сама разложит их по всем ячейкам массива.
    ///
    /// Когда покупатель реально берёт товар (см. CustomerShopper.ShoppingRoutine
    /// / TryTake), прячется (SetActive(false)) только ОДИН случайный ещё
    /// видимый экземпляр - остальные на полке остаются лежать. Через
    /// _restockDelay секунд именно этот экземпляр сам снова показывается (0 -
    /// он больше не появится).
    /// </summary>
    public class ShopShelfStock : MonoBehaviour
    {
        [Tooltip("Какой это товар - нужно, чтобы именно он появился на кассе, когда покупатель его возьмёт")]
        [SerializeField] private ShopItemData _item;
        [Tooltip("Все уже существующие на сцене объекты этого товара, лежащие на полке")]
        [SerializeField] private GameObject[] _displayItems;
        [Tooltip("Через сколько секунд взятый экземпляр появится на полке снова (0 - больше не появится)")]
        [SerializeField] private float _restockDelay = 5f;

        public ShopItemData Data => _item;

        public bool HasStock
        {
            get
            {
                if (_displayItems == null) return false;

                foreach (GameObject item in _displayItems)
                    if (item != null && item.activeSelf)
                        return true;

                return false;
            }
        }

        // Вызывается CustomerShopper, когда покупатель реально берёт этот товар -
        // прячет один случайный ещё видимый экземпляр с полки. Возвращает false,
        // если на полке уже ничего не осталось (например, забрал другой покупатель).
        public bool TryTake()
        {
            var active = new List<GameObject>();
            if (_displayItems != null)
            {
                foreach (GameObject item in _displayItems)
                    if (item != null && item.activeSelf)
                        active.Add(item);
            }

            if (active.Count == 0) return false;

            GameObject taken = active[Random.Range(0, active.Count)];
            taken.SetActive(false);

            if (_restockDelay > 0f)
                StartCoroutine(RestockAfterDelay(taken));

            return true;
        }

        private IEnumerator RestockAfterDelay(GameObject item)
        {
            yield return new WaitForSeconds(_restockDelay);

            if (item != null)
                item.SetActive(true);
        }

        // Мгновенно возвращает на полку один случайный СПРЯТАННЫЙ экземпляр, не
        // дожидаясь _restockDelay - используется, когда пойманного вора заставляют
        // вернуть украденное (см. CustomerThief). Ничего не создаёт заново - просто
        // включает обратно ту же самую (уже существующую) модель. Возвращает false,
        // если прятать было нечего (на полке и так все экземпляры на месте).
        public bool TryRestockOne()
        {
            if (_displayItems == null) return false;

            foreach (GameObject item in _displayItems)
            {
                if (item != null && !item.activeSelf)
                {
                    item.SetActive(true);
                    return true;
                }
            }

            return false;
        }

        // Охранник донёс нужный товар (см. CustomerThief.DropStolenItems) до
        // подходящей полки - если товар совпадает, физическая копия, которую
        // нёс игрок, пропадает В ЛЮБОМ СЛУЧАЕ (чтобы не зависала в руках, если
        // полка вдруг уже сама успела пополниться, пока охранник её нёс - см.
        // ShopShelfStock._restockDelay). Заодно пробуем и правда включить обратно
        // спрятанную модель на полке (TryRestockOne) - если там как раз есть
        // свободное место, полка визуально тоже пополнится. Для срабатывания
        // на объекте полки нужен СВОЙ триггер-коллайдер (отдельно от коллайдеров
        // самих товаров на ней).
        private void OnTriggerEnter(Collider other)
        {
            if (_item == null) return;

            ScannableItem item = other.GetComponentInParent<ScannableItem>();
            if (item == null || item.Data != _item) return;

            TryRestockOne();
            SecurityStats.RegisterItemReturned();
            Destroy(item.gameObject);
        }
    }
}

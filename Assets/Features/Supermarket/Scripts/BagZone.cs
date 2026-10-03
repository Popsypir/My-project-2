using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Триггер-зона упаковки (правая сторона кассы). Засчитывает только уже
    /// отсканированные товары - если предмет ещё не сканировали, зона его игнорирует
    /// (он остаётся лежать, его нужно оттащить обратно к сканеру).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BagZone : MonoBehaviour
    {
        [Tooltip("Необязательно - если не назначено, ищется само на сцене")]
        [SerializeField] private CheckoutManager _checkoutManager;

        private void Awake()
        {
            if (_checkoutManager == null)
                _checkoutManager = FindAnyObjectByType<CheckoutManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<ScannableItem>();
            if (item == null || item.State != ScanState.Scanned) return;

            item.MarkBagged();
            _checkoutManager?.RegisterBagged(item);
        }
    }
}

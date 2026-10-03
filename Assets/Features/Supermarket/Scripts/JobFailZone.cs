using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Дно пропасти за дверью для покупателей. Обслуженные покупатели туда
    /// падают под настоящей физикой (Rigidbody) и убираются отсюда сразу же -
    /// а если сюда падает сам ИГРОК, смена считается проваленной.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class JobFailZone : MonoBehaviour
    {
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("Необязательно - если не назначено, ищется само на сцене")]
        [SerializeField] private CheckoutManager _checkoutManager;

        private void Awake()
        {
            if (_checkoutManager == null)
                _checkoutManager = FindAnyObjectByType<CheckoutManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(_playerTag))
            {
                _checkoutManager?.FailShift();
                return;
            }

            // Не игрок - значит это упавший покупатель (ему тут и место) или
            // случайно закатившийся сюда товар. Через attachedRigidbody, чтобы
            // удалить весь объект целиком, а не только дочернюю часть с
            // коллайдером, если Rigidbody на родителе.
            GameObject toRemove = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

            // Товар, упавший сюда - потерян безвозвратно, засчитываем в статистику
            if (toRemove.GetComponent<ScannableItem>() != null)
                SecurityStats.RegisterItemLost();

            Destroy(toRemove);
        }
    }
}

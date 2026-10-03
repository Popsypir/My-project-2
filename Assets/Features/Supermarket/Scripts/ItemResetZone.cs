using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// "Сетка безопасности". Если товар улетает мимо кассы (например от резкого
    /// броска мышью при перетаскивании) и падает сюда - его не теряем, а
    /// возвращаем обратно на точку возврата вместо того, чтобы он улетал
    /// в бесконечность и становился недоступным.
    ///
    /// Повесь этот компонент на большой плоский Collider (галка Is Trigger),
    /// который перекрывает всю игровую зону снизу, пониже уровня пола.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ItemResetZone : MonoBehaviour
    {
        [Tooltip("Куда возвращать упавший товар. Если пусто - возвращает на эту же точку (transform этого объекта)")]
        [SerializeField] private Transform _resetPoint;

        private void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<ScannableItem>();
            if (item == null || item.Rb == null) return;

            Vector3 target = _resetPoint != null ? _resetPoint.position : transform.position;

            item.Rb.position = target;
            item.Rb.linearVelocity = Vector3.zero;
            item.Rb.angularVelocity = Vector3.zero;
        }
    }
}

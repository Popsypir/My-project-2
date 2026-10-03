using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Вешается на объект с Rigidbody, движением которого управляет скрипт
    /// (например CustomerQueueController). Такие объекты обычно каждый кадр
    /// сами задают себе скорость - из-за этого внешний толчок (например от
    /// игрока) тут же "стирается" в следующем кадре. NotifyPushed() на
    /// короткое время просит владеющий скрипт отпустить контроль, чтобы
    /// толчок физически подействовал.
    /// </summary>
    public class PushStagger : MonoBehaviour
    {
        [SerializeField] private float _staggerDuration = 0.4f;

        private float _staggerUntil;

        public bool IsStaggered => Time.time < _staggerUntil;

        public void NotifyPushed()
        {
            _staggerUntil = Time.time + _staggerDuration;
        }
    }
}

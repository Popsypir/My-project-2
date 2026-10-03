using System;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Базовый класс "поставщика игрового дня". Если в игре уже есть своя система
    /// времени/суток — унаследуйте от неё свой класс от GameDayProviderBase
    /// и переопределите CurrentDay, вызывая RaiseNewDay() при смене дня.
    ///
    /// Сделан абстрактным КЛАССОМ, а не интерфейсом — так поля в инспекторе,
    /// ссылающиеся на него, нельзя случайно перепутать с другим компонентом
    /// (та же причина, по которой PhoneAppBase — класс, а не просто IPhoneApp).
    /// </summary>
    public abstract class GameDayProviderBase : MonoBehaviour
    {
        public abstract int CurrentDay { get; }

        public event Action OnNewDay;

        protected void RaiseNewDay() => OnNewDay?.Invoke();
    }
}

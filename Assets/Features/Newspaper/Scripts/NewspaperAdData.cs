using System;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Одно рекламное объявление — картинка (+ опционально номер).
    /// Не отдельный ассет, а запись внутри списка в NewspaperContentDatabase.
    /// </summary>
    [Serializable]
    public class NewspaperAdData
    {
        [Tooltip("Картинка объявления")]
        public Sprite AdImage;

        [Tooltip("Необязательно. Если номер уже нарисован на картинке — можно оставить пустым")]
        public PhoneNumberEntry Number;
    }
}

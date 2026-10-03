using System.Collections.Generic;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// "Пул" всего, что может попасть в газету. Создаётся как ассет:
    /// правый клик в Project -> Create -> Game Phone -> Newspaper Content Database.
    /// Заполняется один раз в редакторе, а конкретные "выпуски" на каждый день
    /// генерируются из этого пула кодом (см. NewspaperGenerator).
    /// </summary>
    [CreateAssetMenu(menuName = "Game Phone/Newspaper Content Database", fileName = "NewspaperContentDatabase")]
    public class NewspaperContentDatabase : ScriptableObject
    {
        [Tooltip("Все возможные картинки-объявления. Каждый день случайно выбирается несколько из них")]
        public List<NewspaperAdData> AllAds = new();

        [Tooltip("Варианты текста газеты. Каждый день выбирается один случайный вариант")]
        public List<string> NewsTextVariants = new();
    }
}

using System.Collections.Generic;
using System.Linq;

namespace GamePhone
{
    /// <summary>
    /// Генерирует содержимое газеты для конкретного дня. Не зависит от Unity UI —
    /// только данные, поэтому легко проверить/протестировать отдельно.
    ///
    /// Генерация детерминирована: один и тот же день всегда даёт один и тот же
    /// результат (сид случайности = номер дня), даже если открыть газету
    /// несколько раз за день.
    /// </summary>
    public static class NewspaperGenerator
    {
        public static NewspaperDayContent GenerateForDay(int day, NewspaperContentDatabase database, int adSlotCount)
        {
            var rng = new System.Random(day);

            var ads = ShuffleCopy(database.AllAds, rng)
                .Take(adSlotCount)
                .ToList();

            string newsText = database.NewsTextVariants.Count > 0
                ? database.NewsTextVariants[rng.Next(database.NewsTextVariants.Count)]
                : string.Empty;

            return new NewspaperDayContent { NewsText = newsText, Ads = ads };
        }

        private static List<T> ShuffleCopy<T>(List<T> source, System.Random rng)
        {
            var copy = new List<T>(source);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }
    }
}

using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Пул имён для посылок и посетителей. Имя печатается на бирке коробки
    /// (см. Box.RecipientName) и висит над головой посетителя, который пришёл
    /// за своей посылкой (см. PostNpc) - по имени игрок и находит нужную.
    /// </summary>
    public static class ParcelNames
    {
        private static readonly string[] First =
        {
            "Олег", "Витя", "Люся", "Тамара", "Гена", "Зоя", "Боря", "Рита",
            "Паша", "Нина", "Сева", "Клава", "Юра", "Галя", "Кузя", "Марина",
            "Стёпа", "Алла", "Фёдор", "Даша", "Матвей", "Соня", "Гриша", "Вера"
        };

        private static readonly string[] Last =
        {
            "Гвоздев", "Пушкарёв", "Сухов", "Мятликов", "Воронцов", "Балалайкин",
            "Тихонов", "Кирпичёв", "Селёдкин", "Заточкин", "Облаков", "Мухин",
            "Щукин", "Варенов", "Громов", "Пряников", "Лаптев", "Синицын"
        };

        /// <summary>Случайное имя вида "Олег Гвоздев".</summary>
        public static string Random()
        {
            string first = First[UnityEngine.Random.Range(0, First.Length)];
            string last = Last[UnityEngine.Random.Range(0, Last.Length)];

            // фамилии женские по тем же корням, чтобы "Люся Гвоздев" не попадалось
            if (IsFemale(first) && !last.EndsWith("а")) last += "а";

            return first + " " + last;
        }

        private static bool IsFemale(string name) =>
            name.EndsWith("а") || name.EndsWith("я");
    }
}

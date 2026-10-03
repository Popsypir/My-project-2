using System.Collections.Generic;

namespace GamePhone
{
    /// <summary>
    /// Ответы игрока на вопросы в телефонных разговорах (см. CallScript, Answer Key).
    /// Живёт, пока запущена игра, и не сбрасывается при смене сцен.
    /// </summary>
    public static class PlayerAnswers
    {
        private static readonly Dictionary<string, string> _answers = new();

        public static void Set(string key, string value)
        {
            if (!string.IsNullOrEmpty(key))
                _answers[key] = value;
        }

        public static string Get(string key, string fallback = "")
        {
            return key != null && _answers.TryGetValue(key, out string value) ? value : fallback;
        }

        public static bool Has(string key) => key != null && _answers.ContainsKey(key);
    }
}

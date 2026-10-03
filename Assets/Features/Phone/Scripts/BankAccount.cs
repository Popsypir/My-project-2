using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePhone
{
    [Serializable]
    public class BankTransaction
    {
        public string Description;
        public float Amount; // положительное - пополнение, отрицательное - списание
        public string Timestamp; // просто строка (DateTime.Now.ToString), для истории операций
    }

    /// <summary>
    /// Баланс счёта и история операций - сохраняется между запусками через
    /// PlayerPrefs (JSON), как ContactsStorage. Баланс не хранится отдельным
    /// числом - это просто сумма всех операций в истории, так они никогда не
    /// разъедутся между собой.
    /// </summary>
    public static class BankAccount
    {
        private const string PrefsKey = "BankAccount";

        [Serializable]
        private class HistoryWrapper
        {
            public List<BankTransaction> History = new();
        }

        private static List<BankTransaction> _history;

        public static event Action OnChanged;

        public static IReadOnlyList<BankTransaction> History
        {
            get { EnsureLoaded(); return _history; }
        }

        public static float Balance
        {
            get
            {
                EnsureLoaded();
                float sum = 0f;
                foreach (var t in _history)
                    sum += t.Amount;
                return sum;
            }
        }

        // amount - со знаком: положительное число - зарплата/пополнение,
        // отрицательное - списание/покупка. description - что подписать в истории
        public static void AddTransaction(string description, float amount)
        {
            EnsureLoaded();

            _history.Add(new BankTransaction
            {
                Description = description,
                Amount = amount,
                Timestamp = DateTime.Now.ToString("dd.MM HH:mm")
            });

            Save();
        }

        private static void EnsureLoaded()
        {
            if (_history != null) return;

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            var wrapper = string.IsNullOrEmpty(json)
                ? new HistoryWrapper()
                : JsonUtility.FromJson<HistoryWrapper>(json);

            _history = wrapper?.History ?? new List<BankTransaction>();
        }

        private static void Save()
        {
            var wrapper = new HistoryWrapper { History = _history };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
            OnChanged?.Invoke();
        }
    }
}

using TMPro;
using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Одна строка в истории операций банка - описание, сумма (с цветом и
    /// знаком в зависимости от того, пополнение это или списание) и дата.
    /// Создаётся кодом (BankApp.RefreshList) под каждую операцию - само на
    /// себя вешать в сцене не нужно, это шаблон строки (префаб).
    /// </summary>
    public class BankTransactionRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _amountText;
        [Tooltip("Необязательно - когда добавилась операция")]
        [SerializeField] private TMP_Text _dateText;

        [Header("Цвет суммы")]
        [SerializeField] private Color _incomeColor = new Color(0.2f, 0.75f, 0.3f);
        [SerializeField] private Color _expenseColor = new Color(0.85f, 0.25f, 0.25f);

        public void Setup(BankTransaction transaction)
        {
            if (_descriptionText != null)
                _descriptionText.text = transaction.Description;

            if (_amountText != null)
            {
                bool isIncome = transaction.Amount >= 0f;
                string sign = isIncome ? "+" : "-";
                _amountText.text = $"{sign}{Mathf.Abs(transaction.Amount):0}";
                _amountText.color = isIncome ? _incomeColor : _expenseColor;
            }

            if (_dateText != null)
                _dateText.text = transaction.Timestamp;
        }
    }
}

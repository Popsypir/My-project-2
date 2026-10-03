using TMPro;
using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Банк" - строка с текущим балансом (число + кастомная
    /// иконка валюты рядом, просто картинка в интерфейсе, коду её трогать не
    /// нужно) и ниже список истории операций (BankAccount.History), новые
    /// сверху. Баланс/история сохраняются между запусками - см. BankAccount.
    ///
    /// Список строится точно так же, как в ContactsApp - без Layout Group,
    /// карточки фиксированного размера просто расставляются вручную сверху
    /// вниз по высоте самой карточки.
    /// </summary>
    public class BankApp : PhoneAppBase
    {
        [Header("Баланс")]
        [Tooltip("Кастомная иконка валюты кладётся рядом с этим текстом прямо в интерфейсе - " +
                 "отдельная картинка (Image), коду её назначать не нужно")]
        [SerializeField] private TMP_Text _balanceText;
        [Tooltip("Формат числа баланса, см. float.ToString(format). По умолчанию с разделителем тысяч, без копеек")]
        [SerializeField] private string _balanceFormat = "N0";

        [Header("История операций")]
        [Tooltip("Куда добавлять строки операций (Content прокручиваемого списка)")]
        [SerializeField] private RectTransform _rowsContainer;
        [Tooltip("Префаб одной строки истории")]
        [SerializeField] private BankTransactionRowUI _rowPrefab;
        [Tooltip("Отступ между строками по вертикали")]
        [SerializeField] private float _rowSpacing = 4f;

        private void OnEnable()
        {
            BankAccount.OnChanged += HandleAccountChanged;
        }

        private void OnDisable()
        {
            BankAccount.OnChanged -= HandleAccountChanged;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshBalance();
            RefreshList();
        }

        // Баланс/история могли поменяться, пока это приложение даже не было
        // открыто (например пришла зарплата) - на открытии OnOpen и так
        // обновит всё заново, а это - на случай, если что-то изменится ПРЯМО
        // во время просмотра этого экрана.
        private void HandleAccountChanged()
        {
            RefreshBalance();
            RefreshList();
        }

        private void RefreshBalance()
        {
            if (_balanceText != null)
                _balanceText.text = BankAccount.Balance.ToString(_balanceFormat);
        }

        private void RefreshList()
        {
            if (_rowsContainer == null || _rowPrefab == null) return;

            foreach (Transform child in _rowsContainer)
                Destroy(child.gameObject);

            float rowHeight = _rowPrefab.GetComponent<RectTransform>().rect.height;
            float y = 0f;
            int count = 0;

            // Новые операции сверху - идём по истории с конца
            var history = BankAccount.History;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                BankTransactionRowUI row = Instantiate(_rowPrefab, _rowsContainer);
                RectTransform rowRect = row.GetComponent<RectTransform>();

                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -y);

                row.Setup(history[i]);

                y += rowHeight + _rowSpacing;
                count++;
            }

            float totalHeight = count > 0 ? y - _rowSpacing : 0f;
            _rowsContainer.sizeDelta = new Vector2(_rowsContainer.sizeDelta.x, totalHeight);
        }
    }
}

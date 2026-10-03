using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Контакты". Игрок сам вписывает имя+номер и добавляет в
    /// список - список сохраняется между запусками (ContactsStorage). У
    /// каждого контакта есть кнопка быстрого набора: переключает на экран
    /// звонилки, подставляет номер и сразу звонит - используя ТУ ЖЕ логику,
    /// что и ручной набор (CallApp.Call). Если номер ненастоящий (игрок
    /// вписал что попало) - дозвон не пройдёт, как и при ручном наборе.
    /// </summary>
    public class ContactsApp : PhoneAppBase
    {
        [Header("Добавление контакта")]
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TMP_InputField _numberInput;
        [SerializeField] private Button _addButton;

        [Header("Список")]
        [Tooltip("Куда добавлять строки контактов (Content прокручиваемого списка)")]
        [SerializeField] private RectTransform _rowsContainer;
        [Tooltip("Префаб одной строки списка (карточка фиксированного размера, всё внутри неё расставлено вручную)")]
        [SerializeField] private ContactRowUI _rowPrefab;
        [Tooltip("Отступ между карточками по вертикали")]
        [SerializeField] private float _rowSpacing = 8f;

        [Header("Быстрый набор")]
        [SerializeField] private PhoneScreenManager _screenManager;
        [SerializeField] private CallApp _callApp;

        private void Start()
        {
            if (_addButton != null)
                _addButton.onClick.AddListener(HandleAddContact);

            if (_numberInput != null)
                _numberInput.onValueChanged.AddListener(HandleNumberInputChanged);
        }

        // Пока игрок печатает - выкидываем всё, что не цифра, и сразу
        // показываем в формате "+X(XXX)XXX-XX-XX", как в самой звонилке.
        private void HandleNumberInputChanged(string text)
        {
            string digits = PhoneNumberFormat.ExtractDigits(text);
            string formatted = PhoneNumberFormat.FormatForDisplay(digits);

            if (formatted == text) return;

            _numberInput.SetTextWithoutNotify(formatted);
            _numberInput.caretPosition = formatted.Length;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshList();
        }

        private void HandleAddContact()
        {
            string name = _nameInput != null ? _nameInput.text.Trim() : "";
            // Хранить нужно чистые цифры (без +()-), а не то, что видно в поле -
            // иначе быстрый набор не совпадёт с CallApp/PhoneNumberEntry.Number.
            string number = _numberInput != null ? PhoneNumberFormat.ExtractDigits(_numberInput.text) : "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(number))
                return;

            ContactsStorage.Add(name, number);

            if (_nameInput != null) _nameInput.text = "";
            if (_numberInput != null) _numberInput.text = "";

            RefreshList();
        }

        // Никакого Layout Group - карточки строго фиксированного размера (см.
        // префаб), расставляем их вручную сверху вниз по высоте самой карточки.
        // Просто и предсказуемо - ничего "не ломается" от авто-раскладки.
        private void RefreshList()
        {
            if (_rowsContainer == null || _rowPrefab == null) return;

            foreach (Transform child in _rowsContainer)
                Destroy(child.gameObject);

            float rowHeight = _rowPrefab.GetComponent<RectTransform>().rect.height;
            float y = 0f;
            int count = 0;

            foreach (var contact in ContactsStorage.Contacts)
            {
                ContactRowUI row = Instantiate(_rowPrefab, _rowsContainer);
                RectTransform rowRect = row.GetComponent<RectTransform>();

                // Крепим карточку к верхнему краю Content и ставим встык под предыдущей
                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -y);

                row.Setup(contact, HandleQuickDial, HandleDelete);

                y += rowHeight + _rowSpacing;
                count++;
            }

            // Растягиваем сам Content под общую высоту карточек - иначе скролл
            // не будет знать, что там что-то есть ниже видимой области
            float totalHeight = count > 0 ? y - _rowSpacing : 0f;
            _rowsContainer.sizeDelta = new Vector2(_rowsContainer.sizeDelta.x, totalHeight);
        }

        private void HandleQuickDial(PhoneContact contact)
        {
            if (_screenManager == null || _callApp == null)
            {
                Debug.LogWarning("[ContactsApp] Screen Manager или Call App не назначены - быстрый набор не работает", this);
                return;
            }

            // Порядок важен: сначала открыть (CallApp.OnOpen сам чистит номер),
            // потом подставить нужный номер и только затем звонить.
            _screenManager.OpenApp(_callApp);
            _callApp.SetDialedNumber(contact.Number);
            _callApp.Call();
        }

        private void HandleDelete(PhoneContact contact)
        {
            ContactsStorage.Remove(contact);
            RefreshList();
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Одна строка в списке контактов - имя, номер, кнопка быстрого набора и
    /// кнопка удаления. Создаётся кодом (ContactsApp.Instantiate) под каждый
    /// сохранённый контакт, само на себя вешать в сцене не нужно - это шаблон
    /// строки (префаб), не сама строка.
    /// </summary>
    public class ContactRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _numberText;
        [SerializeField] private Button _quickDialButton;
        [SerializeField] private Button _deleteButton;

        public void Setup(PhoneContact contact, Action<PhoneContact> onQuickDial, Action<PhoneContact> onDelete)
        {
            if (_nameText != null) _nameText.text = contact.Name;
            if (_numberText != null) _numberText.text = PhoneNumberFormat.FormatForDisplay(contact.Number);

            if (_quickDialButton != null)
            {
                _quickDialButton.onClick.RemoveAllListeners();
                _quickDialButton.onClick.AddListener(() => onQuickDial?.Invoke(contact));
            }

            if (_deleteButton != null)
            {
                _deleteButton.onClick.RemoveAllListeners();
                _deleteButton.onClick.AddListener(() => onDelete?.Invoke(contact));
            }
        }
    }
}

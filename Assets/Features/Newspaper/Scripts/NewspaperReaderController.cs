using System;
using TMPro;
using UnityEngine;

namespace GamePhone.World
{
    /// <summary>
    /// Полноэкранная газета: 4 картинки-объявления + один текстовый блок новости.
    /// Открывается через NewspaperTableInteractable, закрывается по Escape/кнопке.
    /// Содержимое пересчитывается не чаще раза в игровой день — день двигает
    /// только кровать (BedInteractable), не сама газета.
    /// </summary>
    public class NewspaperReaderController : MonoBehaviour
    {
        [Header("Панель")]
        [Tooltip("Корневой объект экрана газеты - включается/выключается целиком")]
        [SerializeField] private GameObject _root;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        [Header("Данные")]
        [SerializeField] private NewspaperContentDatabase _database;
        [SerializeField] private GameDayProviderBase _dayProvider;

        [Header("UI")]
        [SerializeField] private TMP_Text _newsText;
        [Tooltip("Слоты под картинки объявлений (например 4 штуки)")]
        [SerializeField] private NewspaperAdSlotUI[] _adSlots;

        public bool IsOpen { get; private set; }
        public event Action OnOpened;
        public event Action OnClosed;

        private int _lastGeneratedDay = int.MinValue;

        private void Awake()
        {
            // Если объект газеты в сцене выключен, Awake сработает только при первом
            // Open() - тогда газету уже нельзя прятать обратно
            if (_root != null && !IsOpen)
                _root.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(_closeKey))
                Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            RefreshIfNeeded();

            if (_root != null)
                _root.SetActive(true);

            OnOpened?.Invoke();
        }

        // Повесь этот метод на кнопку "Закрыть", если она есть на экране газеты
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (_root != null)
                _root.SetActive(false);

            OnClosed?.Invoke();
        }

        private void RefreshIfNeeded()
        {
            if (_database == null || _dayProvider == null || _adSlots == null || _adSlots.Length == 0)
                return;

            int today = _dayProvider.CurrentDay;
            if (today == _lastGeneratedDay)
                return;

            var content = NewspaperGenerator.GenerateForDay(today, _database, _adSlots.Length);
            Apply(content);
            _lastGeneratedDay = today;
        }

        private void Apply(NewspaperDayContent content)
        {
            if (_newsText != null)
                _newsText.text = content.NewsText;

            for (int i = 0; i < _adSlots.Length; i++)
            {
                if (i < content.Ads.Count)
                    _adSlots[i].SetAd(content.Ads[i]);
                else
                    _adSlots[i].Clear();
            }
        }
    }
}

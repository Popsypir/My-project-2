using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Управляет сменой продавца: спавнит товары следующего покупателя,
    /// считает сумму текущего покупателя (обнуляется при переходе к следующему)
    /// и ведёт обратный отсчёт времени на всю смену/очередь. Смена заканчивается
    /// ТОЛЬКО по реальному времени (см. StartShift/_shiftEndTime) - лимита на
    /// число обслуженных покупателей нет, сколько бы за смену ни прошло народа.
    /// </summary>
    public class CheckoutManager : MonoBehaviour
    {
        [Header("Системы")]
        [SerializeField] private ConveyorSpawner _conveyor;
        [SerializeField] private CustomerQueueController _queue;
        [Tooltip("Необязательно - чтобы население магазина тоже росло заново с каждой сменой (см. CustomerSpawner)")]
        [SerializeField] private CustomerSpawner _spawner;

        [Header("Время")]
        [Tooltip("Сколько секунд даётся на всю смену/очередь - конец смены считается не накоплением " +
                 "Time.deltaTime, а по-настоящему от реального времени (DateTime.Now), см. StartShift/Update")]
        [SerializeField] private float _shiftDuration = 300f;
        [Tooltip("Пауза между появлением товаров одного покупателя")]
        [SerializeField] private float _spawnInterval = 0.6f;

        [Header("UI")]
        [SerializeField] private TMP_Text _currentTotalText;
        [SerializeField] private TMP_Text _timerText;
        [Tooltip("Раньше показывал \"сколько осталось до лимита\", лимита больше нет (см. класс-докстринг) - " +
                 "теперь просто показывает, сколько покупателей обслужено за смену. Имя поля не переименовывал " +
                 "специально, чтобы не слетело подключение в инспекторе")]
        [SerializeField] private TMP_Text _customersLeftText;

        [Header("Звук")]
        [Tooltip("Откуда проигрывать звуки кассы. Если не назначено - ищется на этом объекте")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Звуки при завершении покупателя (последний товар упакован, как будто печатается чек) - " +
                 "можно добавить несколько вариантов, каждый раз играет случайный, чтобы не приедалось")]
        [SerializeField] private AudioClip[] _receiptSounds;
        [Tooltip("Звуки при успешном завершении всей смены")]
        [SerializeField] private AudioClip[] _shiftEndSounds;
        [Tooltip("Звуки при провале смены (игрок упал в пропасть за дверью)")]
        [SerializeField] private AudioClip[] _shiftFailSounds;

        public event Action OnShiftEnded;
        public event Action OnShiftFailed;

        public bool IsShiftActive => _shiftActive;

        // Для панели итогов смены (см. ShiftEndSequence)
        public int CustomersServed => _customersServed;
        public float ShiftTotalEarned => _shiftTotalEarned;

        private readonly List<ScannableItem> _activeItems = new();
        private DateTime _shiftEndTime;
        private float _timeRemaining;
        private float _currentCustomerTotal;
        private float _shiftTotalEarned;
        private int _customersServed;
        private bool _shiftActive;
        private bool _isSpawning;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (_queue != null)
                _queue.OnCustomerArrivedAtRegister += HandleCustomerArrivedAtRegister;
        }

        private void OnDisable()
        {
            if (_queue != null)
                _queue.OnCustomerArrivedAtRegister -= HandleCustomerArrivedAtRegister;
        }

        private void Start()
        {
            StartShift();
        }

        private void Update()
        {
            if (!_shiftActive) return;

            // Не накапливаем Time.deltaTime кадр за кадром, а честно смотрим на
            // разницу с реальным временем окончания смены (см. StartShift) -
            // "смена заканчивается в момент реальное время + N минут", а не
            // просто абстрактный отсчитываемый таймер.
            _timeRemaining = (float)(_shiftEndTime - DateTime.Now).TotalSeconds;

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                EndShift();
            }

            UpdateTimerUI();
        }

        public void StartShift()
        {
            _shiftEndTime = DateTime.Now.AddSeconds(_shiftDuration);
            _timeRemaining = _shiftDuration;
            _customersServed = 0;
            _currentCustomerTotal = 0f;
            _shiftTotalEarned = 0f;
            _shiftActive = true;

            // Отсчёт стадий "сколько воров разрешено одновременно" (1, 2, 4, 8...)
            // и "сколько покупателей держать в магазине" начинается заново с каждой сменой -
            // как и статистика охранника (см. ShiftEndSequence), иначе панель итогов
            // в конце смены показывала бы накопленное за все предыдущие смены подряд
            TheftDifficultyManager.Instance?.ResetForNewShift();
            _spawner?.ResetForNewShift();
            SecurityStats.ResetAll();

            // Товары не спавним сразу - ждём OnCustomerArrivedAtRegister
            // (покупатель должен реально подойти к кассе).
            UpdateUI();
        }

        // Вызывается AutoCashier - следующий ещё не отсканированный товар с
        // конвейера (или null, если такого сейчас нет)
        public ScannableItem GetNextUnscannedItem()
        {
            foreach (ScannableItem item in _activeItems)
                if (item != null && item.State == ScanState.OnConveyor)
                    return item;

            return null;
        }

        // Вызывается ScannerZone (или AutoCashier)
        public void RegisterScan(ScannableItem item)
        {
            if (!_shiftActive) return;

            _currentCustomerTotal += item.Data.Price;
            _shiftTotalEarned += item.Data.Price;
            UpdateUI();
        }

        // Вызывается BagZone (или AutoCashier)
        public void RegisterBagged(ScannableItem item)
        {
            if (!_shiftActive) return;

            _activeItems.Remove(item);
            Destroy(item.gameObject, 0.2f);

            if (_activeItems.Count == 0 && !_isSpawning)
                CompleteCurrentCustomer();
        }

        private void CompleteCurrentCustomer()
        {
            PlayRandomSound(_receiptSounds);

            _customersServed++;
            _currentCustomerTotal = 0f;
            _queue?.AdvanceQueue();

            // Лимита на число обслуженных больше нет - смена идёт до конца
            // отведённого реального времени (см. Update/_shiftEndTime), сколько
            // бы народа за неё ни обслужили. Товары следующего покупателя не
            // спавним сразу - ждём, пока он реально дойдёт до кассы (см.
            // HandleCustomerArrivedAtRegister).
            UpdateUI();
        }

        // Вызывается CustomerQueueController, когда первый в очереди дошёл до кассы -
        // order - список товаров, которые именно этот покупатель набрал на полках (см. CustomerShopper)
        private void HandleCustomerArrivedAtRegister(List<ShopItemData> order)
        {
            if (!_shiftActive || _isSpawning || _activeItems.Count > 0) return;

            SpawnNextCustomerItems(order);
        }

        private void SpawnNextCustomerItems(List<ShopItemData> order)
        {
            _activeItems.Clear();

            if (_conveyor == null) return;

            StartCoroutine(SpawnOrderRoutine(order ?? new List<ShopItemData>()));
        }

        private IEnumerator SpawnOrderRoutine(List<ShopItemData> order)
        {
            _isSpawning = true;

            yield return StartCoroutine(_conveyor.SpawnItemsStaggered(order, _spawnInterval, item => _activeItems.Add(item)));

            _isSpawning = false;

            // Например пустая база товаров - ничего не заспавнилось, не зависаем на этом покупателе
            if (_activeItems.Count == 0)
                CompleteCurrentCustomer();
        }

        private void EndShift()
        {
            _shiftActive = false;
            PlayRandomSound(_shiftEndSounds);
            OnShiftEnded?.Invoke(); // см. ShiftEndSequence - звонок, покупатели уходят, панель итогов
        }

        // Вызывается JobFailZone, если игрок сам провалился в пропасть за дверью
        public void FailShift()
        {
            if (!_shiftActive) return;

            _shiftActive = false;
            PlayRandomSound(_shiftFailSounds);
            OnShiftFailed?.Invoke();
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (_audioSource == null || clips == null || clips.Length == 0) return;

            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;

            _audioSource.PlayOneShot(clip);
        }

        private void UpdateUI()
        {
            if (_currentTotalText != null)
                _currentTotalText.text = $"{_currentCustomerTotal:0.00}";

            if (_customersLeftText != null)
                _customersLeftText.text = $"{_customersServed}";
        }

        private void UpdateTimerUI()
        {
            if (_timerText == null) return;

            int minutes = Mathf.FloorToInt(_timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(_timeRemaining % 60f);
            _timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}

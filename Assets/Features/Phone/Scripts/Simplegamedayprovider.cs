using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Простая тестовая реализация "игрового дня": день можно двигать вручную
    /// методом AdvanceDay() (повесьте вызов на сон/кровать в игре), либо включить
    /// автоматическое продвижение по таймеру — удобно для проверки газеты.
    /// Когда появится настоящая система дня/ночи — замените этот компонент
    /// своим классом, унаследованным от GameDayProviderBase.
    /// </summary>
    public class SimpleGameDayProvider : GameDayProviderBase
    {
        [SerializeField] private int _startDay = 1;

        [Header("Для теста")]
        [SerializeField] private bool _autoAdvance = false;
        [SerializeField] private float _secondsPerDay = 120f;

        private int _currentDay;
        private float _timer;

        public override int CurrentDay => _currentDay;

        private void Awake()
        {
            _currentDay = _startDay;
        }

        private void Update()
        {
            if (!_autoAdvance) return;

            _timer += Time.deltaTime;
            if (_timer >= _secondsPerDay)
            {
                _timer = 0f;
                AdvanceDay();
            }
        }

        // Вызови этот метод из своей игровой логики, когда должен наступить новый день
        public void AdvanceDay()
        {
            _currentDay++;
            RaiseNewDay();
        }

#if UNITY_EDITOR
        [ContextMenu("Advance Day (Test)")]
        private void AdvanceDayFromEditor() => AdvanceDay();
#endif
    }
}
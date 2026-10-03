using System;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone
{
    /// <summary>
    /// Шуточный "пружинный" слайдер - под капотом самый обычный UnityEngine.UI.Slider
    /// (та же дорожка/ручка, то же перетаскивание мышкой "из коробки"). Пока
    /// тащишь ручку - двигается как у обычного слайдера. Как только отпускаешь -
    /// она не остаётся на месте мгновенно, а по инерции слегка проскакивает
    /// дальше (в ту сторону, куда двигалась рука) и потом пружинисто
    /// покачивается взад-вперёд, пока не успокоится - ПРИМЕРНО там же, где её
    /// отпустили (а не в какой-то одной фиксированной точке на весь слайдер -
    /// цель колебаний каждый раз новая, там, где реально отпустили).
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class SpringSlider : MonoBehaviour
    {
        [Tooltip("Показывает, тащит ли сейчас игрок ползунок - повесь SliderPointerTracker " +
                 "на тот же объект, где сам Slider")]
        [SerializeField] private SliderPointerTracker _pointerTracker;

        [Header("Пружина")]
        [Tooltip("Насколько резко тянет обратно к точке, где отпустили")]
        [SerializeField] private float _springStrength = 40f;
        [Tooltip("Насколько быстро гаснут колебания - больше значение, быстрее успокаивается")]
        [SerializeField] private float _damping = 4f;

        public event Action<float> OnValueChanged;

        private Slider _slider;
        private float _velocity;
        private float _settleTarget;
        private bool _wasDraggingLastFrame;
        private float _previousDragValue;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
        }

        private void Start()
        {
            _settleTarget = _slider.value;
            _slider.onValueChanged.AddListener(HandleSliderChanged);
        }

        // Выставить значение снаружи (например при открытии приложения, подтягивая
        // сохранённую громкость) - без вызова OnValueChanged, чтобы не зациклилось
        public void SetValueWithoutNotify(float value)
        {
            _slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            _settleTarget = _slider.value;
            _velocity = 0f;
        }

        // Настоящий Slider дёргает это и от перетаскивания мышкой, и от наших же
        // программных .value= ниже в Update - в обоих случаях наружу форвардим как есть
        private void HandleSliderChanged(float value)
        {
            OnValueChanged?.Invoke(value);
        }

        private void Update()
        {
            bool isDragging = _pointerTracker != null && _pointerTracker.IsPressed;

            if (isDragging)
            {
                // Прикидываем, с какой скоростью двигали ручку - чтобы отпустив
                // "с разгона", она потом слегка проскочила по инерции, а не
                // просто мгновенно замерла на месте.
                if (_wasDraggingLastFrame)
                    _velocity = (_slider.value - _previousDragValue) / Mathf.Max(Time.deltaTime, 0.0001f);

                _previousDragValue = _slider.value;
                _wasDraggingLastFrame = true;
                return;
            }

            if (_wasDraggingLastFrame)
            {
                // Только что отпустили - цель колебаний ставим именно там, где
                // сейчас отпустили, а не в одну и ту же фиксированную точку.
                _settleTarget = _slider.value;
                _wasDraggingLastFrame = false;
            }

            // Простой демпфированный гармонический осциллятор - тащит значение к
            // _settleTarget, слегка пролетая мимо по инерции и затухая до полной остановки.
            float displacement = _settleTarget - _slider.value;
            _velocity += displacement * _springStrength * Time.deltaTime;
            _velocity *= Mathf.Clamp01(1f - _damping * Time.deltaTime);

            bool settled = Mathf.Abs(_velocity) < 0.0005f && Mathf.Abs(displacement) < 0.0005f;
            float newValue = settled ? _settleTarget : Mathf.Clamp01(_slider.value + _velocity * Time.deltaTime);

            if (settled)
                _velocity = 0f;

            if (!Mathf.Approximately(newValue, _slider.value))
                _slider.value = newValue; // настоящее присвоение - onValueChanged сработает сам, форвардит громкость
        }
    }
}

using System;
using System.Collections;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Отвечает ТОЛЬКО за одно: показать/скрыть панель телефона с анимацией
    /// выезда снизу экрана и обработать ввод (Escape). Ничего не знает
    /// о приложениях, иконках и т.д. — за это отвечают другие классы.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PhoneUIController : MonoBehaviour
    {
        public static PhoneUIController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Animation")]
        [Tooltip("Сколько секунд едет панель")]
        [SerializeField] private float _animDuration = 0.25f;

        [Header("Input")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.Escape;
        [SerializeField] private bool _handleInputInternally = true;

        [SerializeField] private CanvasGroup mainMenuButtons;

        // Позволяет снаружи временно заблокировать реакцию на _toggleKey (например,
        // пока поверх экрана показано своё окно паузы мини-игры - см. PongApp) -
        // чтобы повторный Escape не открывал телефон обратно сам по себе.
        private bool _inputEnabled = true;

        public bool IsOpen { get; private set; }

        public event Action OnOpened;
        public event Action OnClosed;

        private Vector2 _shownPos;
        private Vector2 _hiddenPos;
        private Coroutine _animRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Панель прячем ровно на её собственную высоту вниз от исходной позиции.
            _shownPos = _panel.anchoredPosition;
            _hiddenPos = _shownPos + new Vector2(0f, -_panel.rect.height);

            SetImmediate(false);
        }

        private void Update()
        {
            if (_handleInputInternally && _inputEnabled && Input.GetKeyDown(_toggleKey) && mainMenuButtons.alpha != 1)
                Toggle();
        }

        // См. комментарий у _inputEnabled выше
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            RestartAnimation(_hiddenPos, _shownPos, interactableAtEnd: true);
            OnOpened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            RestartAnimation(_shownPos, _hiddenPos, interactableAtEnd: false);
            OnClosed?.Invoke();
        }

        private void RestartAnimation(Vector2 from, Vector2 to, bool interactableAtEnd)
        {
            if (_animRoutine != null)
                StopCoroutine(_animRoutine);

            _animRoutine = StartCoroutine(AnimatePanel(from, to, interactableAtEnd));
        }

        private IEnumerator AnimatePanel(Vector2 from, Vector2 to, bool interactableAtEnd)
        {
            // Панель видна (alpha = 1) всё время движения — едет как есть, без затухания.
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            float t = 0f;
            while (t < _animDuration)
            {
                // unscaledDeltaTime — чтобы телефон анимировался, даже если игра на паузе (Time.timeScale = 0)
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / _animDuration); // линейно, без кривой ускорения

                _panel.anchoredPosition = Vector2.Lerp(from, to, k);

                yield return null;
            }

            _panel.anchoredPosition = to;
            _canvasGroup.interactable = interactableAtEnd;
            _canvasGroup.blocksRaycasts = interactableAtEnd;
            // Прячем панель мгновенно (не через плавное затухание), когда она уже уехала вниз
            _canvasGroup.alpha = interactableAtEnd ? 1f : 0f;
        }

        private void SetImmediate(bool open)
        {
            IsOpen = open;
            _panel.anchoredPosition = open ? _shownPos : _hiddenPos;
            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }
    }
}

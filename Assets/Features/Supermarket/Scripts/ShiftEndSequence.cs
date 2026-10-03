using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GamePhone.Shop
{
    /// <summary>
    /// Финальная последовательность конца смены: звенит звонок, все покупатели
    /// (где бы каждый сейчас ни находился - на полке, в очереди, в кражe)
    /// разом уходят к выходу, экран игрока притемняется, и сверху съезжает
    /// панель с итогами смены (сколько покупателей обслужено, сколько
    /// заработано, статистика охранника). По кнопке "Продолжить" экран гаснет
    /// и загружается сцена квартиры.
    ///
    /// Подписывается на CheckoutManager.OnShiftEnded - сам ничего не решает
    /// о том, когда смена кончилась, только реагирует.
    /// </summary>
    public class ShiftEndSequence : MonoBehaviour
    {
        [Header("Системы")]
        [SerializeField] private CheckoutManager _checkout;
        [SerializeField] private CustomerQueueController _queue;
        [Tooltip("Куда отправляются покупатели, которых прервали посреди похода по полкам")]
        [SerializeField] private Transform _exitPoint;

        [Header("Звонок")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _bellSound;

        [Header("Затемнение экрана")]
        [Tooltip("Полноэкранная CanvasGroup - НЕ ScreenFader (тот всегда до полной черноты), тут нужно притемнение")]
        [SerializeField] private CanvasGroup _dimOverlay;
        [SerializeField] private float _dimAlpha = 0.7f;
        [SerializeField] private float _dimDuration = 1f;
        [Tooltip("Сколько ждать (пока покупатели уходят), прежде чем показать панель итогов")]
        [SerializeField] private float _customersLeaveWaitTime = 4f;

        [Header("Панель итогов смены")]
        [Tooltip("Сама панель - должна начинаться СВЕРХУ за пределами экрана, уезжает вниз на свою текущую позицию")]
        [SerializeField] private RectTransform _statsPanel;
        [SerializeField] private float _slideDuration = 0.6f;
        [SerializeField] private TMP_Text _customersServedText;
        [SerializeField] private TMP_Text _moneyEarnedText;
        [SerializeField] private TMP_Text _thievesCaughtText;
        [SerializeField] private TMP_Text _thievesEscapedText;
        [SerializeField] private TMP_Text _itemsReturnedText;
        [SerializeField] private TMP_Text _itemsLostText;
        [SerializeField] private Button _continueButton;

        [Header("Переход")]
        [SerializeField] private string _nextSceneName = "Квартирка";

        private Vector2 _statsPanelShownPos;
        private Vector2 _statsPanelHiddenPos;

        private void Awake()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(HandleContinueClicked);

            if (_statsPanel != null)
            {
                _statsPanelShownPos = _statsPanel.anchoredPosition;
                _statsPanelHiddenPos = _statsPanelShownPos + new Vector2(0f, _statsPanel.rect.height + 100f);
                _statsPanel.anchoredPosition = _statsPanelHiddenPos;
                _statsPanel.gameObject.SetActive(false);
            }

            if (_dimOverlay != null)
            {
                _dimOverlay.alpha = 0f;
                _dimOverlay.blocksRaycasts = false;
                _dimOverlay.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_checkout != null)
                _checkout.OnShiftEnded += HandleShiftEnded;
        }

        private void OnDisable()
        {
            if (_checkout != null)
                _checkout.OnShiftEnded -= HandleShiftEnded;
        }

        private void HandleShiftEnded()
        {
            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            // Освобождает курсор (см. PhoneCursorController) и просит контроллеры
            // движения/взгляда игрока замереть (см. CursorRequestState.AnyRequested
            // в SupermarketCharacterMotor/CharacterMotor/FirstPersonCameraController/
            // FixedCameraController/HeadLookController) - на весь конец смены, а
            // не только пока видна сама панель, иначе игрок мог бы разгуливать по
            // уже притемнённому экрану. Release() - см. ContinueRoutine, строго
            // парой, иначе счётчик не сойдётся и курсор останется залипшим навсегда.
            CursorRequestState.Request();

            if (_audioSource != null && _bellSound != null)
                _audioSource.PlayOneShot(_bellSound);

            SendAllCustomersHome();

            if (_dimOverlay != null)
            {
                _dimOverlay.gameObject.SetActive(true);
                _dimOverlay.blocksRaycasts = true;
                yield return Fade(_dimOverlay, 0f, _dimAlpha, _dimDuration);
            }

            yield return new WaitForSeconds(_customersLeaveWaitTime);

            ShowStatsPanel();
        }

        // Разгоняет всех покупателей разом, где бы каждый сейчас ни был:
        // в очереди (включая ещё идущих к своему месту) - CustomerQueueController,
        // ещё на полках - CustomerPopulationTracker.ForceLeave для каждого активного.
        // Ворующих и уже пойманных не трогаем - их выходом занимается своя логика.
        private void SendAllCustomersHome()
        {
            _queue?.ForceAllToExit();

            var trackers = new List<CustomerPopulationTracker>(CustomerPopulationTracker.Active);
            foreach (CustomerPopulationTracker tracker in trackers)
                if (tracker != null)
                    tracker.ForceLeave(_exitPoint);
        }

        private void ShowStatsPanel()
        {
            if (_checkout != null)
            {
                if (_customersServedText != null) _customersServedText.text = $"{_checkout.CustomersServed}";
                if (_moneyEarnedText != null) _moneyEarnedText.text = $"{_checkout.ShiftTotalEarned:0.00}";
            }

            if (_thievesCaughtText != null) _thievesCaughtText.text = $"{SecurityStats.ThievesCaught}";
            if (_thievesEscapedText != null) _thievesEscapedText.text = $"{SecurityStats.ThievesEscaped}";
            if (_itemsReturnedText != null) _itemsReturnedText.text = $"{SecurityStats.ItemsReturned}";
            if (_itemsLostText != null) _itemsLostText.text = $"{SecurityStats.ItemsLost}";

            if (_statsPanel == null) return;

            _statsPanel.gameObject.SetActive(true);
            StartCoroutine(SlidePanel(_statsPanelHiddenPos, _statsPanelShownPos));
        }

        private void HandleContinueClicked()
        {
            StartCoroutine(ContinueRoutine());
        }

        private IEnumerator ContinueRoutine()
        {
            if (_continueButton != null)
                _continueButton.interactable = false;

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeToBlack();

            // Строго перед переходом на новую сцену - иначе счётчик так и
            // останется увеличенным навсегда, и курсор будет залипшим уже в
            // новой сцене (см. Request() в SequenceRoutine)
            CursorRequestState.Release();

            SceneManager.LoadScene(_nextSceneName);
        }

        private IEnumerator SlidePanel(Vector2 from, Vector2 to)
        {
            float t = 0f;

            while (t < _slideDuration)
            {
                t += Time.unscaledDeltaTime;
                _statsPanel.anchoredPosition = Vector2.Lerp(from, to, EaseOutCubic(t / _slideDuration));
                yield return null;
            }

            _statsPanel.anchoredPosition = to;
        }

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            group.alpha = to;
        }
    }
}

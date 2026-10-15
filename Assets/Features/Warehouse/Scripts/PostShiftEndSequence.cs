using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Конец смены на почте. Когда фургон уехал (см. PostOfficeShift.OnVanDeparted),
    /// экран притемняется, сверху съезжает панель с итогами дня, зарплата падает
    /// на счёт игрока (см. BankAccount - её видно в банковском приложении
    /// телефона), и по кнопке "Продолжить" загружается квартира.
    ///
    /// Сам ничего не решает о том, когда смена кончилась - только реагирует,
    /// как ShiftEndSequence в магазине.
    /// </summary>
    public class PostShiftEndSequence : MonoBehaviour
    {
        [Header("Системы")]
        [Tooltip("Смена, окончание которой ждём")]
        [SerializeField] private PostOfficeShift _shift;

        [Header("Затемнение экрана")]
        [Tooltip("Полноэкранная CanvasGroup - НЕ ScreenFader (тот всегда до полной черноты), тут нужно притемнение")]
        [SerializeField] private CanvasGroup _dimOverlay;
        [Tooltip("Насколько притемняется экран: 1 - полная чернота, 0.7 - сквозь неё ещё видно почту")]
        [SerializeField] private float _dimAlpha = 0.7f;
        [Tooltip("Сколько секунд затемняется")]
        [SerializeField] private float _dimDuration = 1f;
        [Tooltip("Пауза перед панелью итогов - чтобы успеть увидеть, как фургон уехал")]
        [SerializeField] private float _delayBeforePanel = 1.5f;

        [Header("Панель итогов")]
        [Tooltip("Сама панель - должна стоять СВЕРХУ за краем экрана, уезжает вниз на свою позицию")]
        [SerializeField] private RectTransform _statsPanel;
        [Tooltip("Сколько секунд едет панель итогов")]
        [SerializeField] private float _slideDuration = 0.6f;
        [Tooltip("Сюда впишется число принятых посылок")]
        [SerializeField] private TMP_Text _parcelsAcceptedText;
        [Tooltip("Сюда впишется число выданных посылок")]
        [SerializeField] private TMP_Text _parcelsGivenText;
        [Tooltip("Сюда впишется число увезённых фургоном посылок")]
        [SerializeField] private TMP_Text _parcelsShippedText;
        [Tooltip("Сюда впишется число ушедших ни с чем посетителей")]
        [SerializeField] private TMP_Text _gaveUpText;
        [Tooltip("Сюда впишется заработок за смену")]
        [SerializeField] private TMP_Text _salaryText;
        [Tooltip("Кнопка «Продолжить» - по ней игрок уходит со смены")]
        [SerializeField] private Button _continueButton;

        [Header("Зарплата")]
        [Tooltip("Начислять ли зарплату на банковский счёт игрока")]
        [SerializeField] private bool _payToBankAccount = true;
        [Tooltip("Подпись операции в истории банка")]
        [SerializeField] private string _payDescription = "Смена на почте";

        [Header("Переход")]
        [Tooltip("Куда перейти после смены. Имя сцены должно быть в списке Build Settings")]
        [SerializeField] private string _nextSceneName = "Квартирка";

        private Vector2 _panelShownPos;
        private Vector2 _panelHiddenPos;
        private bool _finished;
        private bool _salaryPaid;

        private void Awake()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(HandleContinueClicked);

            if (_statsPanel != null)
            {
                _panelShownPos = _statsPanel.anchoredPosition;
                _panelHiddenPos = _panelShownPos + new Vector2(0f, _statsPanel.rect.height + 100f);
                _statsPanel.anchoredPosition = _panelHiddenPos;
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
            if (_shift != null) _shift.OnVanDeparted += HandleVanDeparted;
        }

        private void OnDisable()
        {
            if (_shift != null) _shift.OnVanDeparted -= HandleVanDeparted;
        }

        private void HandleVanDeparted()
        {
            // Смена закрывается ровно один раз: второй показ панели поверх первой
            // начислил бы зарплату ещё раз
            if (_finished) return;
            _finished = true;

            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            // Освобождает курсор и просит контроллеры движения и взгляда замереть
            // на весь конец смены - иначе игрок разгуливал бы по уже притемнённому
            // экрану. Release() строго парой, см. ContinueRoutine: без него счётчик
            // не сойдётся и курсор останется залипшим уже в следующей сцене.
            CursorRequestState.Request();

            if (_dimOverlay != null)
            {
                _dimOverlay.gameObject.SetActive(true);
                _dimOverlay.blocksRaycasts = true;
                yield return Fade(_dimOverlay, 0f, _dimAlpha, _dimDuration);
            }

            yield return new WaitForSeconds(_delayBeforePanel);

            PaySalary();
            ShowStatsPanel();
        }

        private void PaySalary()
        {
            // Строго один раз за смену: случайный повторный вызов не должен
            // выдавать вторую зарплату за ту же работу
            if (_salaryPaid || !_payToBankAccount || _shift == null) return;

            float salary = _shift.Salary;
            if (salary <= 0f) return;

            _salaryPaid = true;
            BankAccount.AddTransaction(_payDescription, salary);
        }

        private void ShowStatsPanel()
        {
            if (_shift != null)
            {
                if (_parcelsAcceptedText != null) _parcelsAcceptedText.text = $"{_shift.ParcelsDelivered}";
                if (_parcelsGivenText != null) _parcelsGivenText.text = $"{_shift.ReceiversServed}";
                if (_parcelsShippedText != null) _parcelsShippedText.text = $"{_shift.ParcelsShipped}";
                if (_gaveUpText != null) _gaveUpText.text = $"{_shift.ReceiversGaveUp}";
                if (_salaryText != null) _salaryText.text = $"{_shift.Salary:0}";
            }

            if (_statsPanel == null) return;

            _statsPanel.gameObject.SetActive(true);
            StartCoroutine(SlidePanel(_panelHiddenPos, _panelShownPos));
        }

        private void HandleContinueClicked() => StartCoroutine(ContinueRoutine());

        private IEnumerator ContinueRoutine()
        {
            if (_continueButton != null)
                _continueButton.interactable = false;

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeToBlack();

            // Строго перед переходом на новую сцену - иначе счётчик так и
            // останется увеличенным, и курсор залипнет уже в квартире
            CursorRequestState.Release();

            SceneManager.LoadScene(_nextSceneName);
        }

        private IEnumerator SlidePanel(Vector2 from, Vector2 to)
        {
            float t = 0f;

            while (t < _slideDuration)
            {
                // unscaledDeltaTime - панель доезжает, даже если игра на паузе
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

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Звонки". Кнопки цифр НЕ отдельный скрипт — просто обычные
    /// UI-кнопки, у которых в Button -> On Click () вызывается CallApp.AppendDigit
    /// с нужной цифрой строкой-параметром прямо в инспекторе (см. гайд).
    /// </summary>
    public class CallApp : PhoneAppBase
    {
        [Header("Экран")]
        [SerializeField] private TMP_Text _displayText;
        [SerializeField] private TMP_Text _feedbackText;

        [Header("Данные")]
        [Tooltip("Все номера, на которые можно дозвониться в игре - это всё вакансии, объявления о них " +
                 "берутся из сегодняшней газеты (см. Newspaper Database/Day Provider ниже)")]
        [SerializeField] private List<PhoneNumberEntry> _knownNumbers;

        [Header("Сегодняшняя газета (чтобы знать, куда сейчас реально нанимают)")]
        [Tooltip("Та же база, что и у NewspaperReaderController")]
        [SerializeField] private NewspaperContentDatabase _newspaperDatabase;
        [SerializeField] private GameDayProviderBase _dayProvider;
        [Tooltip("Должно совпадать с количеством слотов объявлений в самой газете")]
        [SerializeField] private int _adSlotCount = 4;

        [Header("Звонок (озвучка)")]
        [Tooltip("Необязательно. Если не назначен - берётся с этого же объекта")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Поведение")]
        [Tooltip("Закрывать телефон при переносе игрока по номеру Босса (Instant Teleport). " +
                 "Обычные звонки (вакансии) телефон не закрывают")]
        [SerializeField] private bool _closePhoneOnSuccess = true;
        [Tooltip("Сколько секунд длится звонок, если у номера не назначена озвучка (Call Audio)")]
        [SerializeField] private float _noAudioCallDuration = 3f;
        [Tooltip("Сколько секунд держится надпись 'Вызов завершён', прежде чем экран вернётся к набору номера")]
        [SerializeField] private float _callEndedShowTime = 1.5f;
        [Tooltip("Для сцен вроде меню, где реально звонить некуда - набирать номер можно, " +
                 "но при попытке позвонить всегда просто 'нет сигнала', без проверки номеров")]
        [SerializeField] private bool _noSignal = false;

        [SerializeField] private TaskManager taskManager;

        private string _dialedNumber = "";
        private bool _isOpen;
        private bool _inCall;
        private bool _hangUpRequested;
        private Coroutine _callRoutine;
        private bool _teleportFailed;
        private bool _awaitingChoice;
        private bool _rejected;
        private string _pressedDigit;
        private float _callElapsed;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            _isOpen = true;
            ClearNumber();
        }

        public override void OnClose()
        {
            // Ушли с экрана звонилки посреди разговора - это то же, что сбросить вызов
            // (сама корутина остановится вместе с выключением экрана)
            if (_inCall)
            {
                _inCall = false;
                if (_audioSource != null) _audioSource.Stop();
            }

            base.OnClose();
            _isOpen = false;
        }

        private void Update()
        {
            if (!_isOpen || _inCall) return;

            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrlHeld && Input.GetKeyDown(KeyCode.V))
                PasteFromClipboard();
        }

        // Ctrl+V - вставляет номер из буфера обмена, оставляя только цифры
        // (обрезает лишнее вроде "+", пробелов, скобок) и обрезая до MaxDigits.
        private void PasteFromClipboard()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboard)) return;

            string digitsOnly = PhoneNumberFormat.ExtractDigits(clipboard);
            if (digitsOnly.Length == 0) return;

            _dialedNumber = digitsOnly;
            UpdateDisplay();
        }

        // Кнопки 0-9: Button -> On Click () -> CallApp.AppendDigit, параметр строкой = "0".."9"
        public void AppendDigit(string digit)
        {
            // Во время разговора цифры - это ответы на вопрос сценария (см. CallScript)
            if (_inCall)
            {
                if (_awaitingChoice)
                    _pressedDigit = digit;
                return;
            }

            if (_dialedNumber.Length >= PhoneNumberFormat.MaxDigits) return;

            _dialedNumber += digit;
            UpdateDisplay();
        }

        // Кнопка "стереть последнюю цифру"
        public void Backspace()
        {
            // Во время разговора кнопка "стереть" работает как "сбросить вызов"
            if (_inCall)
            {
                HangUp();
                return;
            }

            if (_dialedNumber.Length > 0)
                _dialedNumber = _dialedNumber.Substring(0, _dialedNumber.Length - 1);
            UpdateDisplay();
        }

        // Кнопка "очистить"
        public void ClearNumber()
        {
            _dialedNumber = "";
            SetFeedback(string.Empty);
            UpdateDisplay();
        }

        // Позволяет другим скриптам (например газете) подставить номер заранее
        public void SetDialedNumber(string number)
        {
            _dialedNumber = number ?? "";
            UpdateDisplay();
        }

        // Кнопка "Позвонить". Звонок больше НЕ телепортирует сразу - устраивает
        // на работу "с завтрашнего утра" (см. DayManager.PendingJob) - реально
        // туда игрок попадёт, только когда ляжет спать (DayManager.SleepRoutine).
        public void Call()
        {
            // Кнопка "позвонить" во время разговора - "сбросить"
            if (_inCall)
            {
                HangUp();
                return;
            }

            if (_noSignal)
            {
                SetFeedback("Нет сигнала");
                return;
            }

            var entry = _knownNumbers.Find(e => e != null && e.Number == _dialedNumber);

            if (entry == null)
            {
                SetFeedback("Такого номера не существует");
                return;
            }

            if (!entry.InstantTeleport && !IsAdvertisedToday(entry))
            {
                SetFeedback("Извините, сейчас рабочие не нужны");
                return;
            }

            _callRoutine = StartCoroutine(CallRoutine(entry));
        }

        // Сбросить вызов: останавливает озвучку, результат звонка (найм/перенос) не наступает
        public void HangUp()
        {
            if (!_inCall) return;
            _hangUpRequested = true;
        }

        // Настоящий "телефонный разговор": на экране идёт таймер, играет озвучка
        // (PhoneNumberEntry.CallAudio; если её нет - короткий звонок _noAudioCallDuration).
        // По окончании - "Вызов завершён", как на обычном телефоне, и экран возвращается к набору.
        // Только после нормального окончания разговора срабатывает результат: для номера
        // вакансии запоминается работа на завтра (DayManager.PendingJob), для номера Босса
        // (InstantTeleport) - перенос игрока.
        private IEnumerator CallRoutine(PhoneNumberEntry entry)
        {
            _inCall = true;
            _hangUpRequested = false;
            _awaitingChoice = false;
            _pressedDigit = null;
            _callElapsed = 0f;
            _rejected = false;
            bool rejected = false;

            CallScript script = entry.CallScript;
            if (script != null)
            {
                yield return RunSteps(PickRandomVariant(script.IntroVariants));

                var bankQuestions = script.QuestionBank != null ? script.QuestionBank.Questions : null;
                if (bankQuestions != null)
                {
                    var pool = new List<CallQuestion>(bankQuestions);
                    int toAsk = Mathf.Min(script.QuestionsToAsk, pool.Count);
                    for (int q = 0; q < toAsk && !_hangUpRequested && !_rejected; q++)
                    {
                        int pick = Random.Range(0, pool.Count);
                        CallQuestion question = pool[pick];
                        pool.RemoveAt(pick);
                        yield return RunQuestion(question);
                    }
                }

                rejected = _rejected;
                if (!_hangUpRequested)
                {
                    var endingVariants = rejected ? script.RejectedEndingVariants : script.AcceptedEndingVariants;
                    yield return RunSteps(PickRandomVariant(endingVariants));
                }
            }
            else
            {
                yield return PlayStepClip(entry.CallAudio, _noAudioCallDuration, false);
            }

            bool completed = !_hangUpRequested;
            if (!completed && _audioSource != null)
                _audioSource.Stop();

            _inCall = false;
            SetFeedback(completed ? "Вызов завершён" : "Вызов сброшен");

            // Найм запоминаем сразу по окончании разговора
            if (completed && !entry.InstantTeleport && !rejected)
            {
                DayManager.Instance?.SetPendingJob(entry);

                taskManager.CompleteCurrentTask(1);


                // ==========================================================
            }

            yield return new WaitForSecondsRealtime(_callEndedShowTime);

            _teleportFailed = false;
            if (completed && entry.InstantTeleport)
                yield return StartCoroutine(TeleportRoutine(entry));

            if (!_teleportFailed)
                ClearNumber();
            _callRoutine = null;
        }

        // Выбирает случайный вариант вступления/концовки целиком, чтобы разговор не звучал
        // одинаково при повторных звонках на один и тот же номер
        private List<CallStep> PickRandomVariant(List<CallStepVariant> variants)
        {
            if (variants == null || variants.Count == 0) return null;
            CallStepVariant variant = variants[Random.Range(0, variants.Count)];
            return variant?.Steps;
        }

        // Реплики подряд (вступление / концовка)
        private IEnumerator RunSteps(List<CallStep> steps)
        {
            if (steps == null) yield break;

            foreach (CallStep step in steps)
            {
                if (_hangUpRequested) yield break;
                if (step != null)
                    yield return PlayStepClip(step.Clip, 0f, false);
            }
        }

        // Вопрос из общей базы: играет реплика вопроса, игрок отвечает цифрой (можно,
        // не дожидаясь конца), потом играет реплика-реакция именно на этот ответ
        // (CallChoice.ResponseClip). Нет такой цифры среди ответов или вышло время -
        // вопрос повторяется. Выбранный ответ запоминается (PlayerAnswers), ответ-отказ
        // ставит _rejected.
        private IEnumerator RunQuestion(CallQuestion question)
        {
            if (question == null) yield break;

            // Вопрос без ответов - просто реплика
            if (question.Choices.Count == 0)
            {
                yield return PlayStepClip(question.Clip, 0f, false);
                yield break;
            }

            CallChoice chosen = null;
            while (chosen == null && !_hangUpRequested)
            {
                _pressedDigit = null;
                _awaitingChoice = true;
                yield return PlayStepClip(question.Clip, 0f, true);

                float waited = 0f;
                while (_pressedDigit == null && !_hangUpRequested
                       && (question.ChoiceTimeout <= 0f || waited < question.ChoiceTimeout))
                {
                    waited += Time.unscaledDeltaTime;
                    TickCallTimer();
                    yield return null;
                }

                _awaitingChoice = false;
                if (_pressedDigit != null)
                    chosen = question.Choices.Find(c => c != null && c.Digit == _pressedDigit);
            }

            _awaitingChoice = false;
            if (chosen != null)
            {
                PlayerAnswers.Set(question.AnswerKey, chosen.Label);
                if (chosen.Reject) _rejected = true;

                if (!_hangUpRequested)
                    yield return PlayStepClip(chosen.ResponseClip, 0f, false);
            }
        }

        // Проигрывает реплику, обновляя таймер разговора. Останавливается раньше, если
        // сбросили вызов или (для вопроса, allowChoice) игрок уже нажал цифру.
        private IEnumerator PlayStepClip(AudioClip clip, float fallbackDuration, bool allowChoice)
        {
            float length = clip != null ? clip.length : fallbackDuration;
            if (length <= 0f) yield break;

            if (clip != null && _audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.PlayOneShot(clip);
            }

            float t = 0f;
            while (t < length && !_hangUpRequested && !(allowChoice && _pressedDigit != null))
            {
                t += Time.unscaledDeltaTime;
                TickCallTimer();
                yield return null;
            }

            if (t < length && _audioSource != null)
                _audioSource.Stop();
        }

        private void TickCallTimer()
        {
            _callElapsed += Time.unscaledDeltaTime;
            int total = Mathf.FloorToInt(_callElapsed);
            SetFeedback($"Разговор {total / 60:00}:{total % 60:00}");
        }

        // Номера "Босса" (PhoneNumberEntry.InstantTeleport): после разговора игрок сразу
        // переносится - затемнение -> телепорт -> ждём реального прибытия (сцена может
        // грузиться) -> экран проявляется обратно.
        private IEnumerator TeleportRoutine(PhoneNumberEntry entry)
        {
            var teleport = TeleportService.Instance;
            if (teleport == null)
            {
                _teleportFailed = true;
                SetFeedback("Не удалось дозвониться: нет службы телепортации");
                yield break;
            }

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeToBlack();

            bool arrived = false;
            void HandleArrived(TeleportDestination destination) => arrived = true;
            teleport.OnPlayerTeleported += HandleArrived;

            if (!teleport.TryTeleport(entry, out string failReason))
            {
                teleport.OnPlayerTeleported -= HandleArrived;
                _teleportFailed = true;
                SetFeedback($"Не удалось дозвониться: {failReason}");
                if (ScreenFader.Instance != null)
                    yield return ScreenFader.Instance.FadeFromBlack();
                yield break;
            }

            if (_closePhoneOnSuccess)
                PhoneUIController.Instance?.Close();

            while (!arrived)
                yield return null;

            teleport.OnPlayerTeleported -= HandleArrived;

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeFromBlack();
        }

        // Есть ли этот номер среди объявлений СЕГОДНЯШНЕЙ газеты - та же
        // генерация, что и у самой газеты (NewspaperGenerator детерминирован
        // по номеру дня), просто без UI. Если газета не настроена - не
        // блокируем звонок (считаем рекламируемым всегда), чтобы по ошибке
        // не сломать звонилку там, где газеты вообще нет.
        private bool IsAdvertisedToday(PhoneNumberEntry entry)
        {
            if (_newspaperDatabase == null || _dayProvider == null) return true;

            NewspaperDayContent content = NewspaperGenerator.GenerateForDay(_dayProvider.CurrentDay, _newspaperDatabase, _adSlotCount);
            return content.Ads.Exists(ad => ad != null && ad.Number == entry);
        }

        private void UpdateDisplay()
        {
            if (_displayText != null)
                _displayText.text = PhoneNumberFormat.FormatForDisplay(_dialedNumber);
        }

        private void SetFeedback(string message)
        {
            if (_feedbackText != null)
                _feedbackText.text = message;
        }
    }
}

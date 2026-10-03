using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GamePhone; // нужно для GameDayProviderBase

public class DayManager : GameDayProviderBase
{
    public static DayManager Instance;

    [Header("Счетчик дней")]
    [SerializeField] private int currentDay = 1;
    [SerializeField] private TextMeshProUGUI dayTextUI; // Опционально: UI текст на экране с номером дня ("День 1")

    [Header("Эффект Сна (Затемнение)")]
    [SerializeField] private CanvasGroup fadeCanvasGroup; // Черная панель для плавного затемнения
    [SerializeField] private float fadeDuration = 1.5f;   // Длительность затемнения/проявления

    private bool isSleeping = false;

    // Требуется от GameDayProviderBase - остальная система (например газета)
    // читает именно это свойство, чтобы узнать текущий день.
    public override int CurrentDay => currentDay;

    // На какую работу устроился игрок звонком (см. CallApp) - но ещё не пошёл
    // туда, это случится только когда он ляжет спать (см. SleepRoutine).
    // Сбрасывается сразу после использования, чтобы не сработало повторно
    // на следующий сон, если снова никуда не звонил.
    public PhoneNumberEntry PendingJob { get; private set; }

    // Вызывается CallApp при успешном звонке по объявлению из сегодняшней газеты
    public void SetPendingJob(PhoneNumberEntry job)
    {
        PendingJob = job;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateDayUI();
    }

    // Публичный метод для кнопки "Лечь спать" или триггера кровати
    public void GoToSleep()
    {
        if (!isSleeping)
        {
            StartCoroutine(SleepRoutine());
        }
    }

    private IEnumerator SleepRoutine()
    {
        isSleeping = true;
        Debug.Log("Игрок ложится спать...");

        // 1. Плавное затемнение экрана (Fade In)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            yield return StartCoroutine(Fade(0f, 1f));
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // 2. Увеличиваем день
        currentDay++;
        UpdateDayUI();

        // 3. Уведомляем всех подписчиков (например газету) о наступлении нового дня.
        // Газета на самом деле не обязана слушать это событие - она и сама проверит
        // текущий день при открытии, но событие полезно, если где-то ещё понадобится
        // мгновенная реакция на смену дня.
        RaiseNewDay();

        // Небольшая пауза в темноте
        yield return new WaitForSeconds(0.5f);

        // 3.5. Если вчера устроились на работу звонком (см. CallApp) - утром
        // сразу там, пока экран ещё чёрный. TeleportService.Instance сам решает,
        // нужно ли грузить другую сцену (у каждой работы своя).
        if (PendingJob != null)
            yield return StartCoroutine(TeleportToPendingJob());

        // 4. Плавное проявление экрана (Fade Out). fadeCanvasGroup - объект ЭТОЙ
        // (старой) сцены - если только что телепортировались на работу в
        // ДРУГУЮ сцену (см. TeleportToPendingJob), он уже уничтожен вместе с
        // ней (fadeCanvasGroup == null - Unity сама так помечает уничтоженные
        // объекты) - используем вместо него ScreenFader уже НОВОЙ сцены.
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(Fade(1f, 0f));
            fadeCanvasGroup.gameObject.SetActive(false);
        }
        else if (ScreenFader.Instance != null)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeFromBlack());
        }

        isSleeping = false;
        Debug.Log($"Пробуждение! Наступил День {currentDay}");
    }

    // TeleportService у каждой сцены свой собственный (не переживает смену
    // сцен, см. его докстринг) - поэтому берём именно Instance прямо сейчас,
    // а не храним ссылку заранее: он всегда указывает на актуальный для
    // текущей загруженной сцены (Квартирка, откуда мы засыпаем).
    private IEnumerator TeleportToPendingJob()
    {
        PhoneNumberEntry job = PendingJob;
        PendingJob = null;

        if (TeleportService.Instance == null)
        {
            Debug.LogWarning("[DayManager] Не найден TeleportService - не могу перенести на работу");
            yield break;
        }

        bool arrived = false;
        void HandleArrived(TeleportDestination destination) => arrived = true;
        TeleportService.Instance.OnPlayerTeleported += HandleArrived;

        bool success = TeleportService.Instance.TryTeleport(job, out string failReason);

        if (!success)
        {
            TeleportService.Instance.OnPlayerTeleported -= HandleArrived;
            Debug.LogWarning($"[DayManager] Не удалось перенести на работу '{job.Label}': {failReason}");
            yield break;
        }

        // TryTeleport может вернуть true сразу, но сама телепортация ещё не
        // случилась (например грузится другая сцена) - ждём реального прибытия.
        while (!arrived)
            yield return null;

        TeleportService.Instance.OnPlayerTeleported -= HandleArrived;
    }

    private IEnumerator Fade(float startAlpha, float targetAlpha)
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }

    private void UpdateDayUI()
    {
        if (dayTextUI != null)
        {
            dayTextUI.text = $"День {currentDay}";
        }
    }
}

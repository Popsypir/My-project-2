using UnityEngine;
using System.Collections;

public class TitleAnimation : MonoBehaviour
{
    // ==================== НАПРАВЛЕНИЕ АНИМАЦИИ ====================
    public enum AnimDirection
    {
        FromLeft,   // Вылетает слева
        FromBottom  // Вылетает снизу
    }

    [System.Serializable]
    public class ButtonAnimData
    {
        public RectTransform button;
        public AnimDirection direction;
        [Tooltip("Задержка перед началом анимации этой кнопки")]
        public float delay = 0f;
    }

    // ==================== ЗАГОЛОВОК ====================
    [Header("=== ЗАГОЛОВОК ===")]
    [SerializeField] private RectTransform projectText;
    [SerializeField] private RectTransform myText;

    [Header("Настройки PROJECT (2)")]
    [SerializeField] private float projectFlyDuration = 1.2f;
    [SerializeField] private float projectStartOffsetX = -1200f;

    [Header("Настройки my")]
    [SerializeField] private float myFallDuration = 0.8f;
    [SerializeField] private float myStartOffsetY = 400f;
    [SerializeField] private float bounceHeight = 60f;
    [SerializeField] private float bounceDuration = 0.3f;
    [SerializeField] private float delayBeforeMy = 0.3f;

    [Header("Кривые заголовка")]
    [SerializeField] private AnimationCurve projectEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve myFallEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ==================== КНОПКИ ====================
    [Header("=== КНОПКИ МЕНЮ ===")]
    [SerializeField] private ButtonAnimData[] buttons;

    [Header("Настройки кнопок")]
    [SerializeField] private float buttonFlyDuration = 0.6f;
    [SerializeField] private float buttonStartOffsetLeft = -800f;
    [SerializeField] private float buttonStartOffsetBottom = -600f;
    [SerializeField] private float staggerDelay = 0.12f; // задержка между кнопками
    [SerializeField] private float delayBeforeButtons = 0.5f; // задержка после заголовка

    [Header("Кривые кнопок")]
    [SerializeField] private AnimationCurve buttonEase = AnimationCurve.EaseInOut(0, 0, 0.5f, 1f);

    // ==================== ВНУТРЕННИЕ ПЕРЕМЕННЫЕ ====================
    private Vector3 projectFinalPos;
    private Vector3 myFinalPos;
    private Vector3[] buttonsFinalPos;

    void Start()
    {
        // --- Заголовок ---
        projectFinalPos = projectText.anchoredPosition;
        myFinalPos = myText.anchoredPosition;

        projectText.anchoredPosition = projectFinalPos + new Vector3(projectStartOffsetX, 0, 0);
        myText.anchoredPosition = myFinalPos + new Vector3(0, myStartOffsetY, 0);

        // --- Кнопки ---
        buttonsFinalPos = new Vector3[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttonsFinalPos[i] = buttons[i].button.anchoredPosition;

            if (buttons[i].direction == AnimDirection.FromLeft)
            {
                buttons[i].button.anchoredPosition = buttonsFinalPos[i] + new Vector3(buttonStartOffsetLeft, 0, 0);
            }
            else // FromBottom
            {
                buttons[i].button.anchoredPosition = buttonsFinalPos[i] + new Vector3(0, buttonStartOffsetBottom, 0);
            }

            // Скрываем кнопки до начала анимации
            buttons[i].button.gameObject.SetActive(false);
        }

        // Запуск
        StartCoroutine(AnimateAll());
    }

    private IEnumerator AnimateAll()
    {
        // 1. Анимация заголовка
        yield return StartCoroutine(AnimateTitle());

        // 2. Задержка перед кнопками
        yield return new WaitForSeconds(delayBeforeButtons);

        // 3. Анимация кнопок
        yield return StartCoroutine(AnimateButtons());
    }

    // ==================== АНИМАЦИЯ ЗАГОЛОВКА ====================
    private IEnumerator AnimateTitle()
    {
        yield return StartCoroutine(FlyInFromLeft(projectText, projectFinalPos, projectFlyDuration));
        yield return new WaitForSeconds(delayBeforeMy);
        yield return StartCoroutine(FallWithBounce(myText, myFinalPos, myFallDuration));
    }

    // ==================== АНИМАЦИЯ КНОПОК ====================
    private IEnumerator AnimateButtons()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var data = buttons[i];

            // Ждём индивидуальную задержку кнопки
            yield return new WaitForSeconds(data.delay);

            // Показываем кнопку
            data.button.gameObject.SetActive(true);

            // Запускаем анимацию
            if (data.direction == AnimDirection.FromLeft)
            {
                StartCoroutine(FlyInFromLeft(data.button, buttonsFinalPos[i], buttonFlyDuration));
            }
            else
            {
                StartCoroutine(FlyInFromBottom(data.button, buttonsFinalPos[i], buttonFlyDuration));
            }

            // Задержка между кнопками (stagger)
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    // ==================== КОРУТИНЫ АНИМАЦИЙ ====================

    private IEnumerator FlyInFromLeft(RectTransform target, Vector3 finalPos, float duration)
    {
        Vector3 startPos = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = projectEase.Evaluate(t);

            target.anchoredPosition = Vector3.Lerp(startPos, finalPos, easedT);
            yield return null;
        }

        target.anchoredPosition = finalPos;
    }

    private IEnumerator FlyInFromBottom(RectTransform target, Vector3 finalPos, float duration)
    {
        Vector3 startPos = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = buttonEase.Evaluate(t);

            target.anchoredPosition = Vector3.Lerp(startPos, finalPos, easedT);
            yield return null;
        }

        target.anchoredPosition = finalPos;
    }

    private IEnumerator FallWithBounce(RectTransform target, Vector3 finalPos, float duration)
    {
        Vector3 startPos = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = myFallEase.Evaluate(t);

            float currentY = Mathf.Lerp(startPos.y, finalPos.y, easedT);
            target.anchoredPosition = new Vector3(finalPos.x, currentY, 0);

            yield return null;
        }

        target.anchoredPosition = new Vector3(finalPos.x, finalPos.y, 0);

        yield return StartCoroutine(BounceUp(target, finalPos, bounceHeight, bounceDuration));
        yield return StartCoroutine(BounceDown(target, finalPos, bounceHeight, bounceDuration * 0.7f));
    }

    private IEnumerator BounceUp(RectTransform target, Vector3 basePos, float height, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = target.anchoredPosition;
        Vector3 peakPos = basePos + new Vector3(0, height, 0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            target.anchoredPosition = Vector3.Lerp(startPos, peakPos, easedT);
            yield return null;
        }

        target.anchoredPosition = peakPos;
    }

    private IEnumerator BounceDown(RectTransform target, Vector3 basePos, float height, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = basePos + new Vector3(0, height, 0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t;

            target.anchoredPosition = Vector3.Lerp(startPos, basePos, easedT);
            yield return null;
        }

        target.anchoredPosition = basePos;
    }
}
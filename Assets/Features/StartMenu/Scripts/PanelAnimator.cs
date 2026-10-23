using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public class PanelAnimator : MonoBehaviour
{
    [Header("Основные настройки")]
    [Tooltip("Если выключено, панель появляется и исчезает мгновенно (идеально для панели подтверждения выхода)")]
    public bool useAnimation = true;

    [Header("Параметры анимации")]
    [Tooltip("Общее время открытия")]
    public float openDuration = 0.6f;
    [Tooltip("Общее время закрытия")]
    public float closeDuration = 0.35f;
    [Tooltip("Ширина панели в виде 'тонкой линии'")]
    public float minWidth = 4f;
    [Tooltip("Доля ширины, на которую панель залетает дальше стыка (инерция)")]
    [Range(0.05f, 0.25f)]
    public float overshootRatio = 0.12f;
    [Tooltip("Где находится стык частей панели (0 = левый край, 1 = правый край, 0.5 = середина)")]
    [Range(0.1f, 0.9f)]
    public float jointRatio = 0.5f;

    [Header("Конечное состояние панели")]
    [Tooltip("Конечная позиция (anchoredPosition)")]
    public Vector2 finalPos = new Vector2(0f, 0f);
    [Tooltip("Конечный размер (sizeDelta)")]
    public Vector2 finalSize = new Vector2(600f, 700f);

    [Header("Начальное состояние (откуда вылетает)")]
    [Tooltip("Начальная позиция (обычно далеко слева или справа)")]
    public Vector2 startPos = new Vector2(-1200f, 0f);

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Coroutine currentCoroutine;
    private bool isOpen = false;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        // Инициализация в скрытом состоянии
        ApplyInstantState(false);
    }

    public void PlayOpen()
    {
        if (isOpen) return;
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);

        // Если анимация отключена, применяем состояние мгновенно
        if (!useAnimation)
        {
            ApplyInstantState(true);
            return;
        }

        currentCoroutine = StartCoroutine(AnimateOpen());
    }

    public void PlayClose()
    {
        if (!isOpen) return;
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);

        // Если анимация отключена, применяем состояние мгновенно
        if (!useAnimation)
        {
            ApplyInstantState(false);
            return;
        }

        currentCoroutine = StartCoroutine(AnimateClose());
    }

    /// <summary>
    /// Мгновенное применение состояния без корутин
    /// </summary>
    private void ApplyInstantState(bool open)
    {
        isOpen = open;
        if (open)
        {
            rectTransform.anchoredPosition = finalPos;
            rectTransform.sizeDelta = finalSize;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            rectTransform.anchoredPosition = startPos;
            rectTransform.sizeDelta = new Vector2(minWidth, finalSize.y);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator AnimateOpen()
    {
        isOpen = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float leftEdgeX = finalPos.x - (finalSize.x / 2f);
        float jointX = leftEdgeX + (finalSize.x * jointRatio);
        float overshootX = jointX + (finalSize.x * overshootRatio);

        rectTransform.anchoredPosition = startPos;
        rectTransform.sizeDelta = new Vector2(minWidth, finalSize.y);
        canvasGroup.alpha = 0f;

        float elapsed = 0f;

        // ФАЗА 1: Полет тонкой линией к точке стыка
        float p1Duration = openDuration * 0.45f;
        while (elapsed < p1Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / p1Duration);
            float ease = EaseOutCubic(t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, new Vector2(jointX, startPos.y), ease);
            rectTransform.sizeDelta = new Vector2(minWidth, finalSize.y);
            canvasGroup.alpha = Mathf.Clamp01(t * 3f);

            yield return null;
        }

        // ФАЗА 2: Инерция (залет за стык)
        float p2Duration = openDuration * 0.20f;
        elapsed = 0f;
        Vector2 p2StartPos = new Vector2(jointX, startPos.y);

        while (elapsed < p2Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / p2Duration);
            float ease = EaseOutQuad(t);

            rectTransform.anchoredPosition = Vector2.Lerp(p2StartPos, new Vector2(overshootX, startPos.y), ease);
            float currentWidth = Mathf.Lerp(minWidth, minWidth * 4f, ease);
            rectTransform.sizeDelta = new Vector2(currentWidth, finalSize.y);

            yield return null;
        }

        // ФАЗА 3: Возврат и растяжение до максимума
        float p3Duration = openDuration * 0.35f;
        elapsed = 0f;
        Vector2 p3StartPos = new Vector2(overshootX, startPos.y);
        float p3StartWidth = minWidth * 4f;

        while (elapsed < p3Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / p3Duration);
            float ease = EaseOutBack(t);

            rectTransform.anchoredPosition = Vector2.Lerp(p3StartPos, finalPos, ease);
            float currentWidth = Mathf.Lerp(p3StartWidth, finalSize.x, ease);
            rectTransform.sizeDelta = new Vector2(currentWidth, finalSize.y);

            yield return null;
        }

        rectTransform.anchoredPosition = finalPos;
        rectTransform.sizeDelta = finalSize;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator AnimateClose()
    {
        isOpen = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        Vector2 currentPos = rectTransform.anchoredPosition;
        float currentWidth = rectTransform.sizeDelta.x;

        float leftEdgeX = finalPos.x - (finalSize.x / 2f);
        float jointX = leftEdgeX + (finalSize.x * jointRatio);

        while (elapsed < closeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / closeDuration);
            float ease = EaseInQuad(t);

            rectTransform.anchoredPosition = Vector2.Lerp(currentPos, new Vector2(jointX, currentPos.y), ease);
            float width = Mathf.Lerp(currentWidth, minWidth, ease);
            rectTransform.sizeDelta = new Vector2(width, finalSize.y);
            canvasGroup.alpha = 1f - ease;

            yield return null;
        }

        rectTransform.anchoredPosition = startPos;
        rectTransform.sizeDelta = new Vector2(minWidth, finalSize.y);
        canvasGroup.alpha = 0f;
    }

    private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private float EaseOutQuad(float t) => t * (2f - t);
    private float EaseInQuad(float t) => t * t;
    private float EaseOutBack(float t)
    {
        float s = 1.70158f;
        float t1 = t - 1f;
        return t1 * t1 * ((s + 1f) * t1 + s) + 1f;
    }
}
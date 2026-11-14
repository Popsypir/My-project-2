using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Настройки размера (в пикселях)")]
    [Tooltip("Сколько пикселей добавлять к ширине при наведении")]
    public float hoverWidthAdd = 20f;
    [Tooltip("Сколько пикселей добавлять к высоте при наведении")]
    public float hoverHeightAdd = 20f;
    [Tooltip("Скорость плавного изменения размера")]
    public float sizeSmoothSpeed = 8f;

    [Header("Настройки колебаний (вверх-вниз)")]
    [Tooltip("Амплитуда колебаний в пикселях")]
    public float bobAmplitude = 8f;
    [Tooltip("Частота колебаний")]
    public float bobFrequency = 1.5f;
    [Tooltip("Скорость плавного возврата позиции")]
    public float positionSmoothSpeed = 8f;

    private RectTransform rectTransform;
    private Vector2 originalSize;
    private Vector2 originalPosition;

    private bool isHovered = false;
    private float currentBobOffset = 0f;
    private int originalSiblingIndex = 0;

    // Статическая переменная для отслеживания текущей активной кнопки
    private static ButtonHoverEffect currentHoveredButton = null;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalSize = rectTransform.sizeDelta;
        originalPosition = rectTransform.anchoredPosition;
        originalSiblingIndex = transform.GetSiblingIndex();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Если есть другая активная кнопка, деактивируем её
        if (currentHoveredButton != null && currentHoveredButton != this)
        {
            currentHoveredButton.isHovered = false;
            // Возвращаем старую кнопку на её исходную позицию в иерархии
            currentHoveredButton.transform.SetSiblingIndex(currentHoveredButton.originalSiblingIndex);
        }

        // Активируем текущую кнопку
        currentHoveredButton = this;
        isHovered = true;

        // Выводим кнопку на передний план
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Если текущая кнопка активна, деактивируем её
        if (currentHoveredButton == this)
        {
            currentHoveredButton = null;
            // Возвращаем кнопку на её исходную позицию в иерархии
            transform.SetSiblingIndex(originalSiblingIndex);
        }
        isHovered = false;
    }

    private void Update()
    {
        // --- ЛОГИКА РАЗМЕРА ---
        Vector2 targetSize = isHovered
            ? new Vector2(originalSize.x + hoverWidthAdd, originalSize.y + hoverHeightAdd)
            : originalSize;

        rectTransform.sizeDelta = Vector2.Lerp(rectTransform.sizeDelta, targetSize, Time.deltaTime * sizeSmoothSpeed);

        // --- ЛОГИКА КОЛЕБАНИЙ ---
        float targetBob = isHovered ? Mathf.Sin(Time.time * bobFrequency) * bobAmplitude : 0f;

        currentBobOffset = Mathf.Lerp(currentBobOffset, targetBob, Time.deltaTime * positionSmoothSpeed);

        rectTransform.anchoredPosition = originalPosition + new Vector2(0f, currentBobOffset);
    }
}

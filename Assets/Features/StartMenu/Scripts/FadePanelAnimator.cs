using UnityEngine;
using System.Collections;

public class FadePanelAnimator : MonoBehaviour
{
    [Header("Настройки плавной анимации")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeSpeed = 4f; // Скорость появления/исчезновения

    private Coroutine activeCoroutine;

    private void Awake()
    {
        // Автоматически находим CanvasGroup, если он не назначен вручную
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    // Плавное появление
    public void PlayOpen()
    {
        gameObject.SetActive(true);
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(FadeRoutine(1f, false));
    }

    // Плавное исчезновение
    public void PlayClose()
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(FadeRoutine(0f, true));
    }

    // Мгновенное скрытие (без анимации, для переключения между меню)
    public void CloseInstant()
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool hideOnComplete)
    {
        // Разрешаем взаимодействие, если панель открывается
        if (targetAlpha > 0)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        // Плавно меняем прозрачность
        while (Mathf.Abs(canvasGroup.alpha - targetAlpha) > 0.01f)
        {
            // Используем unscaledDeltaTime, чтобы анимация работала даже если игра на паузе
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // Если панель полностью исчезла, отключаем её и блокируем клики
        if (hideOnComplete)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            gameObject.SetActive(false);
        }

        activeCoroutine = null;
    }
}
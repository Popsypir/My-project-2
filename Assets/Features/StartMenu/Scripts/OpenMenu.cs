using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class OpenMenu : MonoBehaviour
{
    private int isIndex = 0;

    [Header("Аниматоры панелей (Drag & Drop сюда сами панели)")]
    [SerializeField] private PanelAnimator settingsAnimator;
    [SerializeField] private PanelAnimator creditsAnimator;
    [SerializeField] private PanelAnimator exitAnimator;
    [SerializeField] private PanelAnimator mpGamesAnimator;
    [SerializeField] private PanelAnimator loadGamesAnimator;

    [Header("Тексты кнопок")]
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private TextMeshProUGUI settingsTextButton;
    [SerializeField] private TextMeshProUGUI exitTextButton;
    [SerializeField] private TextMeshProUGUI mpGamesTextButton;
    [SerializeField] private TextMeshProUGUI loadGamesTextButton;

    [Header("Кнопки (для EventSystem)")]
    [SerializeField] private GameObject settingsButton;
    [SerializeField] private GameObject creditsButton;
    [SerializeField] private GameObject exitButton;
    [SerializeField] private GameObject mpGamesButton;
    [SerializeField] private GameObject loadGamesButton;

    private string[] originalTexts = new string[6];

    void Start()
    {
        if (settingsTextButton != null) originalTexts[1] = settingsTextButton.text;
        if (authorsTextButton != null) originalTexts[2] = authorsTextButton.text;
        if (exitTextButton != null) originalTexts[3] = exitTextButton.text;
        if (mpGamesTextButton != null) originalTexts[4] = mpGamesTextButton.text;
        if (loadGamesTextButton != null) originalTexts[5] = loadGamesTextButton.text;
    }

    public int getIndex()
    {
        return isIndex;
    }

    public void OpenMenus(int index)
    {
        if (isIndex == index)
        {
            // Закрытие по той же кнопке или ESC -> С АНИМАЦИЕЙ
            Close(index);
            isIndex = 0;
        }
        else
        {
            // Переключение на другую панель -> СТАРАЯ закрывается МГНОВЕННО
            CloseOtherMenus(index);
            Open(index);
        }
    }

    private void Open(int index)
    {
        isIndex = index;
        PanelAnimator targetAnimator = GetAnimatorByIndex(index);
        TextMeshProUGUI targetText = GetTextByIndex(index);

        if (targetAnimator != null)
        {
            targetAnimator.PlayOpen();
        }

        if (targetText != null)
        {
            targetText.text = "НАЗАД";
        }
    }

    // Обычное закрытие (с анимацией, для ESC и кнопки "Назад")
    private void Close(int index)
    {
        PanelAnimator targetAnimator = GetAnimatorByIndex(index);
        TextMeshProUGUI targetText = GetTextByIndex(index);

        if (targetAnimator != null)
        {
            targetAnimator.PlayClose();
        }

        if (targetText != null && index < originalTexts.Length)
        {
            targetText.text = originalTexts[index];
        }

        if (isIndex == index)
        {
            isIndex = 0;
        }
        EventSystem.current.SetSelectedGameObject(null);
    }

    // НОВЫЙ МЕТОД: Мгновенное закрытие (для замены одной панели на другую)
    private void CloseInstant(int index)
    {
        PanelAnimator targetAnimator = GetAnimatorByIndex(index);
        TextMeshProUGUI targetText = GetTextByIndex(index);

        if (targetAnimator != null)
        {
            targetAnimator.CloseInstant(); // Вызываем мгновенное скрытие
        }

        if (targetText != null && index < originalTexts.Length)
        {
            targetText.text = originalTexts[index];
        }
    }

    private void CloseOtherMenus(int activeIndex)
    {
        for (int i = 1; i <= 5; i++)
        {
            if (i != activeIndex)
            {
                // ИЗМЕНЕНО: теперь закрываем без анимации
                CloseInstant(i);
            }
        }
    }

    private void Update()
    {
        if (isIndex != 0)
        {
            GameObject targetButton = GetButtonByIndex(isIndex);
            if (targetButton != null && EventSystem.current.currentSelectedGameObject != targetButton)
            {
                EventSystem.current.SetSelectedGameObject(targetButton);
            }
        }

        // Закрытие по ESC всегда остается с анимацией
        if (Input.GetKeyDown(KeyCode.Escape) && isIndex != 0)
        {
            Close(isIndex);
            isIndex = 0;
        }
    }

    private PanelAnimator GetAnimatorByIndex(int index)
    {
        switch (index)
        {
            case 1: return settingsAnimator;
            case 2: return creditsAnimator;
            case 3: return exitAnimator;
            case 4: return mpGamesAnimator;
            case 5: return loadGamesAnimator;
            default: return null;
        }
    }

    private TextMeshProUGUI GetTextByIndex(int index)
    {
        switch (index)
        {
            case 1: return settingsTextButton;
            case 2: return authorsTextButton;
            case 3: return exitTextButton;
            case 4: return mpGamesTextButton;
            case 5: return loadGamesTextButton;
            default: return null;
        }
    }

    private GameObject GetButtonByIndex(int index)
    {
        switch (index)
        {
            case 1: return settingsButton;
            case 2: return creditsButton;
            case 3: return exitButton;
            case 4: return mpGamesButton;
            case 5: return loadGamesButton;
            default: return null;
        }
    }

    public void OpenLink(int index)
    {
        switch (index)
        {
            case 1:
                Application.OpenURL("https://www.youtube.com/@Popsypir");
                break;
            case 2:
                Application.OpenURL("https://github.com/Popsypir/My-project-2");
                break;
            case 3:
                Application.OpenURL("https://t.me/KartonPop");
                break;
        }
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OpenMenu : MonoBehaviour
{
    // ---------------------- Переменные
    private bool _isSettingsOpen = false;
    private bool _isCreditsOpen = false;
    private bool _isExitOpen = false;
    private bool _isMPGamesOpen = false;
    private bool _isLoadGamesOpen = false;

    private int isIndex = 0;

    // ---------------------- SerializeField
    [Header("Панели")]
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup ExitPanel;
    [SerializeField] private CanvasGroup MPGamesPanel;
    [SerializeField] private CanvasGroup LoadGamesPanel;

    [Header("Тексты кнопок")]
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private TextMeshProUGUI SettingsTextButton;
    [SerializeField] private TextMeshProUGUI ExitTextButton;
    [SerializeField] private TextMeshProUGUI MPGamesTextButton;
    [SerializeField] private TextMeshProUGUI LoadGamesTextButton;

    [Header("Кнопки")]
    [SerializeField] private GameObject SettingsButton;
    [SerializeField] private GameObject CreditsButton;
    [SerializeField] private GameObject ExitButton;
    [SerializeField] private GameObject MPGamesButton;
    [SerializeField] private GameObject LoadGamesButton;

    public void OpenMenus(int index)
    {
        switch (index)
        {
            case 1:
                if (_isSettingsOpen) { CloseSettings(); isIndex = 0; }       // Если настройки уже открыты - закрываем
                else { CloseOtherMenus(1); OpenSettings(); }     // Производим закрытие остальных меню и открытие настроек
                break;
            case 2:
                if (_isCreditsOpen) { CloseCredits(); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(2); OpenCredits(); }         
                break;
            case 3:
                if (_isExitOpen) { CloseExit(); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(3); OpenExit(); }
                break;
            case 4:
                if (_isMPGamesOpen) { CloseMPGames(); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(4); OpenMPGames(); }
                break;
            case 5:
                if (_isLoadGamesOpen) { CloseLoadGames(); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(5); OpenLoadGames(); }
                break;
        }
    }

// ----------------- Открытия, закрытия меню
    private void OpenSettings()
    {
        isIndex = 1;
        settingsPanel.alpha = 1.0f;
        settingsPanel.interactable = true;
        _isSettingsOpen = true;
        settingsPanel.blocksRaycasts = true;
        SettingsTextButton.text = "НАЗАД";
    }

    private void CloseSettings()
    {
        settingsPanel.alpha = 0f;
        settingsPanel.interactable = false;
        _isSettingsOpen = false;
        settingsPanel.blocksRaycasts = false;
        SettingsTextButton.text = "НАСТРОЙКИ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenMPGames()
    {
        isIndex = 4;
        MPGamesPanel.alpha = 1.0f;
        MPGamesPanel.interactable = true;
        _isMPGamesOpen = true;
        MPGamesPanel.blocksRaycasts = true;
        MPGamesTextButton.text = "НАЗАД";
    }

    private void CloseMPGames()
    {
        MPGamesPanel.alpha = 0f;
        MPGamesPanel.interactable = false;
        _isMPGamesOpen = false;
        MPGamesPanel.blocksRaycasts = false;
        MPGamesTextButton.text = "СЕТЕВАЯ ИГРА";
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenLoadGames()
    {
        isIndex = 5;
        LoadGamesPanel.alpha = 1.0f;
        LoadGamesPanel.interactable = true;
        _isLoadGamesOpen = true;
        LoadGamesPanel.blocksRaycasts = true;
        LoadGamesTextButton.text = "НАЗАД";
    }

    private void CloseLoadGames()
    {
        LoadGamesPanel.alpha = 0f;
        LoadGamesPanel.interactable = false;
        _isLoadGamesOpen = false;
        LoadGamesPanel.blocksRaycasts = false;
        LoadGamesTextButton.text = "ЗАГРУЗИТЬ ИГРУ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenExit()
    {
        isIndex = 3;
        ExitPanel.alpha = 1.0f;
        ExitPanel.interactable = true;
        _isExitOpen = true;
        ExitPanel.blocksRaycasts = true;
        ExitTextButton.text = "НАЗАД";
    }

    public void CloseExit()
    {
        ExitPanel.alpha = 0f;
        ExitPanel.interactable = false;
        _isExitOpen = false;
        ExitPanel.blocksRaycasts = false;
        ExitTextButton.text = "ВЫЙТИ ИЗ ИГРЫ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    public int getIndex()
    {
        return isIndex;
    }

    private void OpenCredits()
    {
        isIndex = 2;
        creditsPanel.alpha = 1.0f;
        creditsPanel.interactable = true;
        creditsPanel.blocksRaycasts = true;
        _isCreditsOpen = true;
        authorsTextButton.text = "НАЗАД";
    }

    private void CloseCredits()
    {
        creditsPanel.alpha = 0;
        creditsPanel.interactable = false;
        creditsPanel.blocksRaycasts = false;
        _isCreditsOpen = false;
        authorsTextButton.text = "ТИТРЫ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void CloseOtherMenus(int index)
    {
        switch (index)
        {
            case 1: CloseCredits(); CloseExit(); CloseMPGames(); CloseLoadGames(); break;   // Закрываем все остальные меню
            case 2: CloseSettings(); CloseExit(); CloseMPGames(); CloseLoadGames(); break;
            case 3: CloseSettings(); CloseCredits(); CloseMPGames(); CloseLoadGames(); break;
            case 4: CloseSettings(); CloseCredits(); CloseExit(); CloseLoadGames(); break;
            case 5: CloseSettings(); CloseCredits(); CloseExit(); CloseMPGames(); break;

        }
    }

    private void Update()
    {
        if (_isSettingsOpen || _isCreditsOpen || _isExitOpen || _isMPGamesOpen || _isLoadGamesOpen) // Проверка на открыто ли хотя бы одно меню
        {
            if (_isSettingsOpen && EventSystem.current.currentSelectedGameObject != SettingsButton)        // Проверка на селект* кнопки (если она не селекнута* - селектим*) 
            {                                                                                              // * - выбор от англ. select
                EventSystem.current.SetSelectedGameObject(SettingsButton);
            }

            if (_isCreditsOpen && EventSystem.current.currentSelectedGameObject != CreditsButton)  // аналогично
            {
                EventSystem.current.SetSelectedGameObject(CreditsButton);
            }

            if (_isExitOpen && EventSystem.current.currentSelectedGameObject != ExitButton)  // аналогично
            {
                EventSystem.current.SetSelectedGameObject(ExitButton);
            }

            if (_isMPGamesOpen && EventSystem.current.currentSelectedGameObject != MPGamesButton)  // аналогично
            {
                EventSystem.current.SetSelectedGameObject(MPGamesButton);
            }

            if (_isLoadGamesOpen && EventSystem.current.currentSelectedGameObject != LoadGamesButton)  // аналогично
            {
                EventSystem.current.SetSelectedGameObject(LoadGamesButton);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && (_isCreditsOpen || _isSettingsOpen || _isExitOpen || _isMPGamesOpen || _isLoadGamesOpen)) // Если хотя бы одно меню открыто и нажата кнопка ESC
        {
            if (_isSettingsOpen == true) { CloseSettings(); isIndex = 0; }    // Проверка на конкретное меню и закрытие
            if (_isCreditsOpen == true) { CloseCredits(); isIndex = 0; }
            if (_isExitOpen == true) { CloseExit(); isIndex = 0; }
            if (_isMPGamesOpen == true) { CloseMPGames(); isIndex = 0; }
            if (_isLoadGamesOpen == true) { CloseLoadGames(); isIndex = 0; }
        }
    }
}

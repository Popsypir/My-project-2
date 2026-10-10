using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OpenMenu : MonoBehaviour
{
    // ---------------------- Переменные
    private bool _isSettingsOpen = false;
    private bool _isCreditsOpen = false;
    private bool _isMiniGamesOpen = false;
    private bool _isMPGamesOpen = false;
    private bool _isLoadGamesOpen = false;

    // ---------------------- SerializeField
    [Header("Панели")]
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup miniGamesPanel;
    [SerializeField] private CanvasGroup MPGamesPanel;
    [SerializeField] private CanvasGroup LoadGamesPanel;

    [Header("Тексты кнопок")]
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private TextMeshProUGUI SettingsTextButton;
    [SerializeField] private TextMeshProUGUI MiniGamesTextButton;
    [SerializeField] private TextMeshProUGUI MPGamesTextButton;
    [SerializeField] private TextMeshProUGUI LoadGamesTextButton;

    [Header("Кнопки")]
    [SerializeField] private GameObject SettingsButton;
    [SerializeField] private GameObject CreditsButton;
    [SerializeField] private GameObject MiniGamesButton;
    [SerializeField] private GameObject MPGamesButton;
    [SerializeField] private GameObject LoadGamesButton;

    public void OpenMenus(int index)
    {
        switch (index)
        {
            case 1:
                if (_isSettingsOpen) { CloseSettings(); }       // Если настройки уже открыты - закрываем
                else { CloseOtherMenus(1); OpenSettings(); }     // Производим закрытие остальных меню и открытие настроек
                break;
            case 2:
                if (_isCreditsOpen) { CloseCredits(); }     // аналогично с настройками
                else { CloseOtherMenus(2); OpenCredits(); }         
                break;
            case 3:
                if (_isMiniGamesOpen) { CloseMiniGames(); }     // аналогично с настройками
                else { CloseOtherMenus(3); OpenMiniGames(); }
                break;
            case 4:
                if (_isMPGamesOpen) { CloseMPGames(); }     // аналогично с настройками
                else { CloseOtherMenus(4); OpenMPGames(); }
                break;
            case 5:
                if (_isLoadGamesOpen) { CloseLoadGames(); }     // аналогично с настройками
                else { CloseOtherMenus(5); OpenLoadGames(); }
                break;
        }
    }

// ----------------- Открытия, закрытия меню
    private void OpenSettings()
    {
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

    private void OpenMiniGames()
    {
        miniGamesPanel.alpha = 1.0f;
        miniGamesPanel.interactable = true;
        _isMiniGamesOpen = true;
        miniGamesPanel.blocksRaycasts = true;
        MiniGamesTextButton.text = "НАЗАД";
    }

    private void CloseMiniGames()
    {
        miniGamesPanel.alpha = 0f;
        miniGamesPanel.interactable = false;
        _isMiniGamesOpen = false;
        miniGamesPanel.blocksRaycasts = false;
        MiniGamesTextButton.text = "МИНИ-ИГРЫ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenCredits()
    {
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
            case 1: CloseCredits(); CloseMiniGames(); CloseMPGames(); CloseLoadGames(); break;   // Закрываем все остальные меню
            case 2: CloseSettings(); CloseMiniGames(); CloseMPGames(); CloseLoadGames(); break;
            case 3: CloseSettings(); CloseCredits(); CloseMPGames(); CloseLoadGames(); break;
            case 4: CloseSettings(); CloseCredits(); CloseMiniGames(); CloseLoadGames(); break;
            case 5: CloseSettings(); CloseCredits(); CloseMiniGames(); CloseMPGames(); break;

        }
    }

    private void Update()
    {
        if (_isSettingsOpen || _isCreditsOpen || _isMiniGamesOpen || _isMPGamesOpen || _isLoadGamesOpen) // Проверка на открыто ли хотя бы одно меню
        {
            if (_isSettingsOpen && EventSystem.current.currentSelectedGameObject != SettingsButton)        // Проверка на селект* кнопки (если она не селекнута* - селектим*) 
            {                                                                                              // * - выбор от англ. select
                EventSystem.current.SetSelectedGameObject(SettingsButton);
            }

            if (_isCreditsOpen && EventSystem.current.currentSelectedGameObject != CreditsButton)  // аналогично
            {
                EventSystem.current.SetSelectedGameObject(CreditsButton);
            }

            if (_isMiniGamesOpen && EventSystem.current.currentSelectedGameObject != MiniGamesButton)  // аналогично
            {
                EventSystem.current.SetSelectedGameObject(MiniGamesButton);
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

        if (Input.GetKeyDown(KeyCode.Escape) && (_isCreditsOpen || _isSettingsOpen || _isMiniGamesOpen || _isMPGamesOpen || _isLoadGamesOpen)) // Если хотя бы одно меню открыто и нажата кнопка ESC
        {
            if (_isSettingsOpen == true) { CloseSettings(); }    // Проверка на конкретное меню и закрытие
            if (_isCreditsOpen == true) { CloseCredits(); }
            if (_isMiniGamesOpen == true) { CloseMiniGames(); }
            if (_isMPGamesOpen == true) { CloseMPGames(); }
            if (_isLoadGamesOpen == true) { CloseLoadGames(); }
        }
    }
}

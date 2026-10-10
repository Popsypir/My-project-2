using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OpenMenu : MonoBehaviour
{
    // ---------------------- Переменные
    private bool _isSettingsOpen = false;
    private bool _isCreditsOpen = false;

    // ---------------------- SerializeField
    [Header("Панели")]
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private CanvasGroup settingsPanel;

    [Header("Тексты кнопок")]
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private TextMeshProUGUI SettingsTextButton;

    [Header("Кнопки")]
    [SerializeField] private GameObject SettingsButton;
    [SerializeField] private GameObject CreditsButton;

    public void OpenMenus(int index)
    {
        switch (index)
        {
            case 1:
                if (_isSettingsOpen) { CloseSettings(); }
                else
                {
                    CloseOtherMenus(1);
                    OpenSettings();              
                }     
                break;
            case 2:
                if (_isCreditsOpen) { CloseCredits(); }
                else 
                {
                    CloseOtherMenus(2);
                    OpenCredits();                   
                }         
                break;
        }
    }

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
            case 1:
                CloseCredits(); break;
            case 2:
                CloseSettings(); break;
        }
    }

    private void Update()
    {
        if (_isSettingsOpen || _isCreditsOpen)
        {
            if (_isSettingsOpen && EventSystem.current.currentSelectedGameObject != SettingsButton)
            {
                EventSystem.current.SetSelectedGameObject(SettingsButton);
            }

            if (_isCreditsOpen && EventSystem.current.currentSelectedGameObject != CreditsButton)
            {
                EventSystem.current.SetSelectedGameObject(CreditsButton);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && (_isCreditsOpen || _isSettingsOpen))
        {
            if (_isSettingsOpen == true) { CloseSettings(); }
            if (_isCreditsOpen == true) { CloseCredits(); }
        }
    }
}

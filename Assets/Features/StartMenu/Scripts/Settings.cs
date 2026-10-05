using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    private bool SettingsOpen = false;
    [Header("Меню настроек")]
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup MainMenuButtons;
    [SerializeField] private TextMeshProUGUI SettingsTextButton;
    [SerializeField] private CanvasGroup Phone;
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private GameObject SettingsButton;

    public void Choise()
    {
        if (settingsPanel.alpha == 0 && creditsPanel.alpha != 1)
        {
            OpenSettings();
        }
        else
        {
            CloseSettings();
        }
    }

    public void OpenSettings()
    {
        settingsPanel.alpha = 1.0f;
        settingsPanel.interactable = true;
        SettingsOpen = true;
        settingsPanel.blocksRaycasts = true;

        SettingsTextButton.text = "НАЗАД";
    }

    void CloseSettings()
    {
        settingsPanel.alpha = 0f;
        settingsPanel.interactable = false;
        SettingsOpen = false;
        settingsPanel.blocksRaycasts = false;

        SettingsTextButton.text = "НАСТРОЙКИ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void SetMainMenuMode()
    {
        if (MainMenuButtons.alpha == 1)
        {
            MainMenuButtons.alpha = 0f;
            MainMenuButtons.interactable = false;
            MainMenuButtons.blocksRaycasts = false;
            Phone.alpha = 1.0f;
            Phone.interactable = true;
            Phone.blocksRaycasts = true;
        }
        else
        {
            MainMenuButtons.alpha = 1f;
            MainMenuButtons.interactable = true;
            MainMenuButtons.blocksRaycasts = true;
            Phone.alpha = 0f;
            Phone.interactable = false;
            Phone.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (SettingsOpen == true && EventSystem.current.currentSelectedGameObject != SettingsButton)
        {
            EventSystem.current.SetSelectedGameObject(SettingsButton);
        }
    }
}

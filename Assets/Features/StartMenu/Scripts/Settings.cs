using UnityEngine;

public class Settings : MonoBehaviour
{
    private bool SettingsOpen = false;
    [Header("Меню настроек")]
    [SerializeField] private CanvasGroup settingsPanel;

    [SerializeField] private CanvasGroup MainMenuButtons;
    [SerializeField] private CanvasGroup Phone;

    public void OpenSettings()
    {
        settingsPanel.alpha = 1.0f;
        settingsPanel.interactable = true;
        settingsPanel.blocksRaycasts = true;
        SettingsOpen = true;
    }

    void CloseSettings()
    {
        settingsPanel.alpha = 0f;
        settingsPanel.interactable = false;
        settingsPanel.blocksRaycasts = false;
        SettingsOpen = false;
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && SettingsOpen == true)
        {
            CloseSettings();
        }
    }
}

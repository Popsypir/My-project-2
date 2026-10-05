using UnityEngine;

public class Settings : MonoBehaviour
{
    private bool SettingsOpen = false;
    [Header("Меню настроек")]
    [SerializeField] private CanvasGroup settingsPanel;

    [Header("UI Элементы")]
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

    public void SetMenuMode(bool mode)
    {
        if (mode == false)
        {
            MainMenuButtons.alpha = 1.0f;
            MainMenuButtons.interactable = true;
            MainMenuButtons.blocksRaycasts = true;
            Phone.alpha = 0;
            Phone.interactable = false;
            Phone.blocksRaycasts = false;
        }

        if (mode == true)
        {
            MainMenuButtons.alpha = 0f;
            MainMenuButtons.interactable = false;
            MainMenuButtons.blocksRaycasts = false;
            Phone.alpha = 1;
            Phone.interactable = true;
            Phone.blocksRaycasts = true;
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

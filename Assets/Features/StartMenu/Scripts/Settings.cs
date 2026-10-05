using UnityEngine;

public class Settings : MonoBehaviour
{
    private bool SettingsOpen = false;
    [Header("Меню настроек")]
    [SerializeField] private CanvasGroup settingsPanel;

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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && SettingsOpen == true)
        {
            CloseSettings();
        }
    }
}

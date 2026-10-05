using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Credits : MonoBehaviour
{
    private bool CreditsOpen = false;
    [Header("Меню титров")]
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private TextMeshProUGUI settingsTextButton;
    [SerializeField] private CanvasGroup settingsPanel;


    [SerializeField] private GameObject CreditsButton;


    public void Choise()
    {
        if (creditsPanel.alpha == 0)
        {
            OpenCredits();
        }
        else
        {
            CloseCredits();
        }
    }

    public void OpenCredits()
    {
        if (settingsPanel.alpha == 1)
        {
            settingsPanel.alpha = 0;
            settingsPanel.interactable = false;
            settingsPanel.blocksRaycasts = false;
            settingsTextButton.text = "НАСТРОЙКИ";

        }
        creditsPanel.alpha = 1.0f;
        creditsPanel.interactable = true;
        creditsPanel.blocksRaycasts = true;
        CreditsOpen = true;

        authorsTextButton.text = "НАЗАД";
    }

    public void CloseCredits()
    {
        creditsPanel.alpha = 0;
        creditsPanel.interactable = false;
        creditsPanel.blocksRaycasts = false;
        CreditsOpen = false;

        authorsTextButton.text = "ТИТРЫ";
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (creditsPanel.alpha != 1)
        {
            CreditsOpen = false;
        }
        if (CreditsOpen == true && EventSystem.current.currentSelectedGameObject != CreditsButton)
        {
            EventSystem.current.SetSelectedGameObject(CreditsButton);
        }

        if (Input.GetKeyDown(KeyCode.Escape) && CreditsOpen == true)
        {
            CloseCredits();
        }
    }
}

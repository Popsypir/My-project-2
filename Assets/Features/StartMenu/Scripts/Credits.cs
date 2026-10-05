using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Credits : MonoBehaviour
{
    private bool CreditsOpen = false;
    [Header("Меню титров")]
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private CanvasGroup settingsPanel;


    [SerializeField] private GameObject CreditsButton;


    public void Choise()
    {
        if (creditsPanel.alpha == 0 && settingsPanel.alpha != 1)
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
        if (CreditsOpen == true && EventSystem.current.currentSelectedGameObject != CreditsButton)
        {
            EventSystem.current.SetSelectedGameObject(CreditsButton);
        }
    }
}

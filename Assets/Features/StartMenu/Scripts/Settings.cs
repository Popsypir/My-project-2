using UnityEngine;

public class Settings : MonoBehaviour
{
    [Header("Главное Меню")]
    [SerializeField] private CanvasGroup MainMenuButtons;

    [Header("Телефон")]
    [SerializeField] private CanvasGroup Phone;

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

}

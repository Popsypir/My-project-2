using GamePhone;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameApp : MonoBehaviour
{

    [Header("Сцена для загрузки")]
    public string targetSceneName = "Квартирка";

    [Header("Меню подтверждения выхода из игры")]
    [SerializeField] private CanvasGroup exitPanel;

    [Header("Меню настроек")]
    [SerializeField] private OpenMenu Openmenu;
    [SerializeField] private CanvasGroup MainMenuButtons;

    [SerializeField] private CanvasGroup LimitSaveWarning;

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && Openmenu.getIndex() == 0)
        {    
            exitPanel.alpha = 1;
            exitPanel.interactable = true;
            exitPanel.blocksRaycasts = true; 
        }
    }

    public void HideWarning()
    {
        LimitSaveWarning.alpha = 0;
        LimitSaveWarning.interactable = false;
        LimitSaveWarning.blocksRaycasts = false;
    }

    public void LoadLocationByName(string sceneName)
    {
        if (SaveSystem.CountSlots() == 6)
        {
            LimitSaveWarning.alpha = 1;
            LimitSaveWarning.interactable = true;
            LimitSaveWarning.blocksRaycasts = true;
        }

        else
        {
            string target = string.IsNullOrWhiteSpace(sceneName) ? targetSceneName : sceneName;

            TaskManager.StartNewGame();

            if (string.IsNullOrWhiteSpace(target))
            {
                Debug.LogError($"[StartGameApp] > Имя сцены не указано!");
                return;
            }

            if (ScreenFader.Instance != null)
            {
                StartCoroutine(LoadWithFade(target));
            }

            else
            {
                Debug.Log($"[StartGameApp] > ScreenFader не найден.");
                SceneManager.LoadScene(target);
            }
        }

    }

    private IEnumerator LoadWithFade(string sceneName)
    {
        yield return ScreenFader.Instance.FadeToBlack();
        SceneManager.LoadScene(sceneName);
    }
}

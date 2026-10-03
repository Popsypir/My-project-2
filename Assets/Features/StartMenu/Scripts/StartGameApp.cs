using GamePhone;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameApp : MonoBehaviour
{
    [Header("Сцена для загрузки")]
    public string targetSceneName = "Квартирка";

    public void LoadLocationByName(string sceneName)
    {
        string target = string.IsNullOrWhiteSpace(sceneName) ? targetSceneName : sceneName;

        SaveSystem.DeleteSave();

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

    private IEnumerator LoadWithFade(string sceneName)
    {
        yield return ScreenFader.Instance.FadeToBlack();
        SceneManager.LoadScene(sceneName);
    }
}

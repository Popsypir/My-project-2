using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private const string SAVE_FILE_NAME = "task_progress.json";
    public static void SaveTaskProgress(int currentTaskIndex)
    {
        SaveData data = new SaveData
        {
            currentTaskIndex = currentTaskIndex
        };
        string json = JsonUtility.ToJson(data, true);
        string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        File.WriteAllText(filePath, json);

        Debug.Log($"[SaveSystem] Прогресс сохранён в: {filePath}");
    }
    public static int LoadTaskProgress()
    {
        string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return data.currentTaskIndex;
        }
        return 0;
    }
    public static void DeleteSave()
    {
        string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log("[SaveSystem] Файл сохранения удалён.");
        }
    }
}
[System.Serializable]
public class SaveData
{
    public int currentTaskIndex;
}

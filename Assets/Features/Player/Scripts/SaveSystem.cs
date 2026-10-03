using UnityEngine;
using System.IO;

public static class SaveSystem
{
    // Имя файла сохранения
    private const string SAVE_FILE_NAME = "task_progress.json";

    /// <summary>
    /// Сохраняет текущий индекс задачи в JSON файл
    /// </summary>
    public static void SaveTaskProgress(int currentTaskIndex)
    {
        // Создаём объект для сохранения
        SaveData data = new SaveData
        {
            currentTaskIndex = currentTaskIndex
        };

        // Превращаем объект в JSON-строку (true делает его читаемым для человека)
        string json = JsonUtility.ToJson(data, true);

        // Получаем безопасный путь для сохранения (работает на всех платформах)
        string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        // Записываем в файл
        File.WriteAllText(filePath, json);

        Debug.Log($"[SaveSystem] Прогресс сохранён в: {filePath}");
    }

    /// <summary>
    /// Загружает индекс задачи из JSON файла
    /// </summary>
    public static int LoadTaskProgress()
    {
        string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        // Если файл существует, читаем его
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            Debug.Log($"[SaveSystem] Прогресс загружен. Текущая задача: {data.currentTaskIndex}");
            return data.currentTaskIndex;
        }

        // Если файла нет (первый запуск), возвращаем 0 (первая задача)
        Debug.Log("[SaveSystem] Файл сохранения не найден. Начинаем с первой задачи.");
        return 0;
    }

    /// <summary>
    /// Удаляет файл сохранения (полезно для тестов или кнопки "Сбросить прогресс")
    /// </summary>
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

// Этот класс должен быть снаружи и иметь атрибут [Serializable], 
// чтобы JsonUtility мог с ним работать.
[System.Serializable]
public class SaveData
{
    public int currentTaskIndex;
}

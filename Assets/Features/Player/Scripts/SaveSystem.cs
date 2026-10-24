using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public static class SaveSystem
{
    private const string SAVE_FILE_NAME = "saves.json";
    private const int MAX_SLOTS = 20;

    [Serializable]
    public class SaveSlotData
    {
        public int slotIndex;
        public int currentTaskIndex;
        public string lastTaskTitle;
        public string lastTaskDescription;
        public string timestamp;
        public string pendingJobNumber = "";
    }

    [Serializable]
    public class SaveContainer
    {
        public List<SaveSlotData> slots = new List<SaveSlotData>();
    }

    private static string FilePath =>
        Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

    private static SaveContainer _cache;
    private static SaveContainer Container
    {
        get
        {
            if (_cache == null) LoadContainer();
            return _cache;
        }
    }

    private static void LoadContainer()
    {
        if (File.Exists(FilePath))
        {
            string json = File.ReadAllText(FilePath);
            _cache = JsonUtility.FromJson<SaveContainer>(json);
            if (_cache == null) _cache = new SaveContainer();
        }
        else
        {
            _cache = new SaveContainer();
        }
    }

    private static void SaveToFile()
    {
        string json = JsonUtility.ToJson(Container, true);
        File.WriteAllText(FilePath, json);
    }

    public static int CreateNewSlot(string taskTitle, string taskDescription = "", string pendingJobNumber = "")
    {
        if (Container.slots.Count >= MAX_SLOTS)
            Container.slots.RemoveAt(0);

        for (int i = 0; i < Container.slots.Count; i++)
            Container.slots[i].slotIndex = i + 1;

        int newIndex = Container.slots.Count + 1;

        var slot = new SaveSlotData
        {
            slotIndex = newIndex,
            currentTaskIndex = 0,
            lastTaskTitle = taskTitle,
            lastTaskDescription = taskDescription,
            pendingJobNumber = pendingJobNumber,
            timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
        };

        Container.slots.Add(slot);
        SaveToFile();

        Debug.Log($"[SaveSystem] Создан слот #{newIndex}");
        return newIndex;
    }

    public static void UpdateSlot(int slotIndex, int taskIndex, string taskTitle, string taskDescription = "", string pendingJobNumber = null)
    {
        var slot = Container.slots.Find(s => s.slotIndex == slotIndex);
        if (slot != null)
        {
            slot.currentTaskIndex = taskIndex;
            slot.lastTaskTitle = taskTitle;
            slot.lastTaskDescription = taskDescription;
            if (pendingJobNumber != null)
                slot.pendingJobNumber = pendingJobNumber;
            slot.timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            SaveToFile();
        }
    }

    public static SaveSlotData LoadSlot(int slotIndex)
    {
        var slot = Container.slots.Find(s => s.slotIndex == slotIndex);
        return slot;
    }

    public static List<SaveSlotData> GetAllSlots()
    {
        return new List<SaveSlotData>(Container.slots);
    }

    public static void DeleteSlot(int slotIndex)
    {
        Container.slots.RemoveAll(s => s.slotIndex == slotIndex);
        for (int i = 0; i < Container.slots.Count; i++)
            Container.slots[i].slotIndex = i + 1;
        SaveToFile();
    }

    public static int SlotCount => Container.slots.Count;

    public static int LoadTaskProgress()
    {
        if (Container.slots.Count == 0) return 0;
        return Container.slots[Container.slots.Count - 1].currentTaskIndex;
    }

    public static void SaveTaskProgress(int currentTaskIndex)
    {
        if (Container.slots.Count > 0)
        {
            var last = Container.slots[Container.slots.Count - 1];
            UpdateSlot(last.slotIndex, currentTaskIndex, last.lastTaskTitle, last.lastTaskDescription, last.pendingJobNumber);
        }
    }

    public static void SavePendingJob(string pendingJobNumber)
    {
        if (Container.slots.Count == 0) return;
        var last = Container.slots[Container.slots.Count - 1];
        UpdateSlot(last.slotIndex, last.currentTaskIndex, last.lastTaskTitle, last.lastTaskDescription, pendingJobNumber);
    }

    public static string LoadPendingJobNumber()
    {
        if (Container.slots.Count == 0) return "";
        return Container.slots[Container.slots.Count - 1].pendingJobNumber ?? "";
    }

    public static string LoadPendingJobNumberFromSlot(int slotIndex)
    {
        var slot = Container.slots.Find(s => s.slotIndex == slotIndex);
        return slot?.pendingJobNumber ?? "";
    }
}
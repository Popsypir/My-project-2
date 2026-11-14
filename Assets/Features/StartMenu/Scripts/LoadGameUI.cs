using UnityEngine;
using System.Collections.Generic;

public class LoadGameUI : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private GameObject saveSlotPrefab;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject emptySlotsMessage;

    private List<GameObject> _activeSlots = new List<GameObject>();

    private void OnEnable()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        ClearSlots();
        var allSlots = SaveSystem.GetAllSlots();

        if (allSlots.Count == 0)
        {
            if (emptySlotsMessage != null) emptySlotsMessage.SetActive(true);
            return;
        }

        if (emptySlotsMessage != null) emptySlotsMessage.SetActive(false);

        // Последние сверху
        for (int i = allSlots.Count - 1; i >= 0; i--)
        {
            var slotData = allSlots[i];
            GameObject slotInstance = Instantiate(saveSlotPrefab, slotsContainer);

            // Используем компонент SaveSlotUI вместо ручного поиска по именам
            var slotUI = slotInstance.GetComponent<SaveSlotUI>();
            if (slotUI != null)
            {
                slotUI.Initialize(slotData, OnSlotClicked, OnSlotDeleted);
            }
            else
            {
                Debug.LogWarning($"[LoadGameUI] На префабе {saveSlotPrefab.name} " +
                                 "не найден компонент SaveSlotUI");
            }

            _activeSlots.Add(slotInstance);
        }
    }

    private void OnSlotClicked(int slotNumber)
    {
        var slotData = SaveSystem.LoadSlot(slotNumber);
        if (slotData != null)
        {
            // ← НОВОЕ: восстановить накопленное время
            if (GameTimeTracker.Instance != null)
                GameTimeTracker.Instance.LoadFromSlot(slotData.playTimeSeconds);

            TaskManager.LoadFromSlot(slotData);
        }
    }

    // ← НОВОЕ: обработчик удаления
    private void OnSlotDeleted(int slotIndex)
    {
        SaveSystem.DeleteSlot(slotIndex); // удаляет + перестраивает нумерацию
        RefreshSlots();                   // обновляет UI
    }

    private void ClearSlots()
    {
        foreach (var slot in _activeSlots)
            if (slot != null) Destroy(slot);
        _activeSlots.Clear();
    }
}
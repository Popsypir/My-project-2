using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

            // Настраиваем тексты по именам
            var texts = slotInstance.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text.name.Contains("Slot") || text.name.Contains("Number"))
                    text.text = $"СОХРАНЕНИЕ {slotData.slotIndex}";
                else if (text.name.Contains("Last") || text.name.Contains("Task"))
                    text.text = $"{slotData.lastTaskTitle}";
            }

            // Кнопка загрузки
            var buttons = slotInstance.GetComponentsInChildren<Button>();
            foreach (var btn in buttons)
            {
                int slotNum = slotData.slotIndex;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSlotClicked(slotNum));
            }

            _activeSlots.Add(slotInstance);
        }
    }

    private void OnSlotClicked(int slotNumber)
    {
        var slotData = SaveSystem.LoadSlot(slotNumber);
        if (slotData != null)
        {
            TaskManager.LoadFromSlot(slotData);
        }
    }

    private void ClearSlots()
    {
        foreach (var slot in _activeSlots)
            if (slot != null) Destroy(slot);
        _activeSlots.Clear();
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private TextMeshProUGUI timerText; // объект "Timer"
    [SerializeField] private TextMeshProUGUI slotNumberText;   // "СОХРАНЕНИЕ 1"
    [SerializeField] private TextMeshProUGUI lastTaskLabel;    // "Последнее задание:" (фиксированное)
    [SerializeField] private TextMeshProUGUI lastTaskText;     // название задачи (динамическое)
    [SerializeField] private TextMeshProUGUI moneyLabel;       // "Количество денег:" (фиксированное)
    [SerializeField] private TextMeshProUGUI moneyText;        // значение денег (динамическое)
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    private SaveSystem.SaveSlotData _slotData;
    private System.Action<int> _onLoadClicked;
    private System.Action<int> _onDeleteClicked;

    /// <summary>
    /// Инициализирует ячейку данными слота.
    /// </summary>
    public void Initialize(SaveSystem.SaveSlotData data,
                           System.Action<int> onLoad,
                           System.Action<int> onDelete = null)
    {
        _slotData = data;
        _onLoadClicked = onLoad;
        _onDeleteClicked = onDelete;

        // Номер слота
        if (slotNumberText != null)
            slotNumberText.text = $"СОХРАНЕНИЕ {data.slotIndex}";

        // Фиксированные лейблы
        if (lastTaskLabel != null)
            lastTaskLabel.text = "Последнее задание:";
        if (moneyLabel != null)
            moneyLabel.text = "Количество денег:";
        // Таймер
        if (timerText != null)
            timerText.text = GameTimeTracker.FormatTime(data.playTimeSeconds);

        // Динамические значения
        string taskDisplay = string.IsNullOrEmpty(data.lastTaskTitle)
            ? "Нет данных"
            : data.lastTaskTitle;
        if (lastTaskText != null)
            lastTaskText.text = taskDisplay;

        if (moneyText != null)
            moneyText.text = $"{data.moneyCount}$";

        // Кнопка загрузки
        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => _onLoadClicked?.Invoke(data.slotIndex));
        }

        // Кнопка удаления (скрывается, если делегат не передан)
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(onDelete != null);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => _onDeleteClicked?.Invoke(data.slotIndex));
        }
    }

    public SaveSystem.SaveSlotData GetSlotData() => _slotData;
}
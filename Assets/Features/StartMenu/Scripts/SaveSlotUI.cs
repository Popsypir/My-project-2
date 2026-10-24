using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private TextMeshProUGUI slotNumberText;  // "СОХРАНЕНИЕ 1"
    [SerializeField] private TextMeshProUGUI lastTaskText;    // "НАЗВАНИЕ ПОСЛЕДНЕЙ ЗАДАЧИ"
    [SerializeField] private Button loadButton;               // Кнопка загрузки
    [SerializeField] private Button deleteButton;             // Кнопка удаления (опционально)

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

        // Отображаем номер слота
        slotNumberText.text = $"СОХРАНЕНИЕ {data.slotIndex}";

        // Отображаем название последней задачи
        // Если задача завершена (индекс >= кол-ва задач), показываем "Все задачи выполнены"
        string taskDisplay = string.IsNullOrEmpty(data.lastTaskTitle)
            ? "Нет данных"
            : data.lastTaskTitle;

        // Можно добавить индекс для наглядности (как на скриншоте)
        lastTaskText.text = $"{taskDisplay}";

        // Подписываем кнопки
        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => _onLoadClicked?.Invoke(data.slotIndex));
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(onDelete != null);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => _onDeleteClicked?.Invoke(data.slotIndex));
        }
    }

    /// <summary>
    /// Возвращает данные слота (на случай если нужны).
    /// </summary>
    public SaveSystem.SaveSlotData GetSlotData() => _slotData;
}

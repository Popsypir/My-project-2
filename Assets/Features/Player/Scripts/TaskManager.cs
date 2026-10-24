using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class TaskManager : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private GameObject taskPanel;
    [SerializeField] private TextMeshProUGUI taskTitle;
    [SerializeField] private TextMeshProUGUI taskDescription;
    [SerializeField] private Image checkbox;
    [SerializeField] private Image checkmark;

    [Header("Настройки")]
    [SerializeField] private float panelMoveDistance = 500f;
    [SerializeField] private float moveDuration = 0.3f;

    private TaskData[] tasks;
    private int currentTaskIndex = 0;
    private bool isPanelMoving = false;
    private bool isPanelHidden = false;
    private Vector3 visiblePos;
    private Vector3 hiddenPos;
    private Vector3 animStartPos;
    private Vector3 animTargetPos;
    private Color currentColor;
    private float moveTimer = 0f;
    private static int _activeSlotIndex = 0;
    private static TaskManager _instance;

    [System.Serializable]
    public class TaskData
    {
        public string title;
        public string description;
    }

    public static readonly TaskData[] DefaultTasks =
    {
        new TaskData { title = "За работу!", description = "Прочитать газету" },
        new TaskData { title = "За работу!", description = "Позвонить и устроиться\nна одну из работ" },
        new TaskData { title = "За работу!", description = "Поспать перед первой\nсменой" },
        new TaskData { title = "Первая смена", description = "Завершить смену" }
    };

    public static event System.Action ProgressChanged;
    private static int _liveProgress = -1;
    private static string _pendingJobNumber = "";

    public static int Progress => _liveProgress >= 0 ? _liveProgress : SaveSystem.LoadTaskProgress();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _liveProgress = -1;
        _activeSlotIndex = 0;
        ProgressChanged = null;
    }

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        if (tasks == null || tasks.Length == 0)
            tasks = DefaultTasks;

        taskPanel.SetActive(true);
        visiblePos = taskPanel.transform.localPosition;
        hiddenPos = visiblePos + new Vector3(panelMoveDistance, 0, 0);

        if (_activeSlotIndex > 0 && _liveProgress >= 0)
        {
            currentTaskIndex = _liveProgress;
            Debug.Log($"[TaskManager] Загружено из слота #{_activeSlotIndex}, задача #{currentTaskIndex}, работа: '{_pendingJobNumber}'");

            // Восстанавливаем работу в DayManager
            if (!string.IsNullOrEmpty(_pendingJobNumber) && currentTaskIndex >= 1 && currentTaskIndex < 3)
            {
                RestorePendingJob();
            }
        }
        else
        {
            currentTaskIndex = SaveSystem.LoadTaskProgress();
            _liveProgress = currentTaskIndex;
            _pendingJobNumber = SaveSystem.LoadPendingJobNumber();
            Debug.Log($"[TaskManager] Загружен номер работы: '{_pendingJobNumber}'");

            if (!string.IsNullOrEmpty(_pendingJobNumber) && currentTaskIndex >= 1 && currentTaskIndex < 3)
            {
                RestorePendingJob();
            }
        }

        ProgressChanged?.Invoke();
        ShowCurrentTask();
    }

    private void Update()
    {
        if (isPanelMoving)
        {
            moveTimer += Time.deltaTime;
            float t = Mathf.Clamp01(moveTimer / moveDuration);
            taskPanel.transform.localPosition = Vector3.Lerp(animStartPos, animTargetPos, t);
            if (t >= 1f) isPanelMoving = false;
        }

        if (Input.GetKeyDown(KeyCode.Tab) && !isPanelMoving && !GamePhone.GameplayInputLock.Blocked)
        {
            if (!isPanelHidden) HidePanel();
            else ShowPanel();
        }
    }

    // ══════════════════════════════════════════
    //  НОВАЯ ИГРА
    // ═══════════════════════════════════════════
    public static void StartNewGame()
    {
        string title = DefaultTasks.Length > 0 ? DefaultTasks[0].title : "Новая игра";
        string desc = DefaultTasks.Length > 0 ? DefaultTasks[0].description : "";

        _activeSlotIndex = SaveSystem.CreateNewSlot(title, desc);
        _liveProgress = 0;
        ProgressChanged?.Invoke();

        Debug.Log($"[TaskManager] Новая игра. Слот #{_activeSlotIndex}");
        SceneManager.LoadScene("Квартирка");
    }

    // ═══════════════════════════════════════════
    //  ЗАГРУЗКА ИЗ СЛОТА
    // ═══════════════════════════════════════════
    public static void LoadFromSlot(SaveSystem.SaveSlotData slotData)
    {
        if (slotData == null) return;
        _activeSlotIndex = slotData.slotIndex;
        _liveProgress = slotData.currentTaskIndex;
        _pendingJobNumber = slotData.pendingJobNumber ?? ""; // ← ДОБАВИТЬ
        ProgressChanged?.Invoke();
        Debug.Log($"[TaskManager] Загрузка слота #{slotData.slotIndex} | Задача: {slotData.currentTaskIndex} | Работа: '{_pendingJobNumber}'");
        SceneManager.LoadScene("Квартирка");
    }

    // ═══════════════════════════════════════════
    //  Сохранение выбранной работы
    // ═══════════════════════════════════════════
    public void SaveCurrentProgress()
    {
        if (_activeSlotIndex <= 0)
        {
            Debug.LogWarning("[TaskManager] Нет активного слота!");
            return;
        }

        int progressToSave = _liveProgress >= 0 ? _liveProgress : currentTaskIndex;

        string taskTitle = (progressToSave < tasks.Length)
            ? tasks[progressToSave].title : "Все задачи выполнены";
        string taskDesc = (progressToSave < tasks.Length)
            ? tasks[progressToSave].description : "";

        SaveSystem.UpdateSlot(_activeSlotIndex, progressToSave, taskTitle, taskDesc, _pendingJobNumber);
    }

    public static void ExitToMenu()
    {
        if (_instance != null)
            _instance.SaveCurrentProgress();

        SceneManager.LoadScene("Menu");
    }

    private void HidePanel()
    {
        isPanelMoving = true;
        isPanelHidden = true;
        moveTimer = 0f;
        animStartPos = taskPanel.transform.localPosition;
        animTargetPos = hiddenPos;
    }

    private void ShowPanel()
    {
        isPanelMoving = true;
        isPanelHidden = false;
        moveTimer = 0f;
        animStartPos = taskPanel.transform.localPosition;
        animTargetPos = visiblePos;
    }

    private void ShowCurrentTask()
    {
        if (currentTaskIndex >= tasks.Length)
        {
            isPanelHidden = true;
            return;
        }

        currentColor = checkmark.color;
        currentColor.a = 0f;
        checkmark.color = currentColor;

        TaskData task = tasks[currentTaskIndex];
        taskTitle.text = task.title;
        taskTitle.color = Color.gold;
        taskTitle.fontStyle = FontStyles.Normal;
        taskDescription.text = task.description.Trim();
        taskDescription.color = Color.white;
        taskDescription.fontStyle = FontStyles.Normal;

        isPanelHidden = false;
        taskPanel.transform.localPosition = visiblePos;
    }

    public void CompleteCurrentTask(int index)
    {
        if (currentTaskIndex >= tasks.Length) return;
        if (index == currentTaskIndex)
        {
            if (index == 2)
            {
                taskTitle.color = Color.green;
                taskTitle.fontStyle = FontStyles.Strikethrough;
            }

            taskDescription.color = Color.green;
            taskDescription.fontStyle = FontStyles.Strikethrough;

            currentColor = checkmark.color;
            currentColor.a = 1f;
            checkmark.color = currentColor;

            currentTaskIndex++;
            _liveProgress = currentTaskIndex;
            ProgressChanged?.Invoke();

            SaveCurrentProgress();
            Invoke(nameof(NextTask), 1.5f);
        }
    }

    private void NextTask()
    {
        ShowCurrentTask();
    }

    private void OnApplicationQuit()
    {
        SaveCurrentProgress();
    }

    /// <summary>
    /// Вызывается из CallApp при успешном звонке.
    /// </summary>
    /// <summary>
    /// Вызывается из CallApp при успешном звонке.
    /// </summary>
    public void SetPendingJob(GamePhone.PhoneNumberEntry job)
    {
        if (job == null) return;
        _pendingJobNumber = job.Number;
        SaveSystem.SavePendingJob(_pendingJobNumber);
        Debug.Log($"[TaskManager] Сохранена работа: {job.Number} ({job.Label})");
    }

    /// <summary>
    /// Восстанавливает PendingJob в DayManager из сохранённого номера.
    /// </summary>
    private void RestorePendingJob()
    {
        if (string.IsNullOrEmpty(_pendingJobNumber)) return;

        // Пробуем CallApp.Instance сначала
        var callApp = GamePhone.Apps.CallApp.Instance;

        // Если Instance null (объект неактивен), ищем через FindObjectOfType с includeInactive
        if (callApp == null)
        {
            callApp = FindObjectOfType<GamePhone.Apps.CallApp>(true);
        }

        if (callApp == null)
        {
            Debug.LogWarning("[TaskManager] CallApp не найден даже через FindObjectOfType(true)");
            return;
        }

        // Ищем через рефлексию (поле _knownNumbers приватное)
        var field = typeof(GamePhone.Apps.CallApp).GetField("_knownNumbers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogWarning("[TaskManager] Не удалось получить _knownNumbers");
            return;
        }

        var knownNumbers = field.GetValue(callApp) as System.Collections.Generic.List<GamePhone.PhoneNumberEntry>;
        if (knownNumbers == null) return;

        foreach (var entry in knownNumbers)
        {
            if (entry != null && entry.Number == _pendingJobNumber)
            {
                if (DayManager.Instance != null)
                {
                    DayManager.Instance.SetPendingJob(entry);
                    Debug.Log($"[TaskManager] ✅ Восстановлена работа: {entry.Number} ({entry.Label})");
                }
                return;
            }
        }

        Debug.LogWarning($"[TaskManager] Работа '{_pendingJobNumber}' не найдена");
    }
}
using UnityEngine;
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

    [System.Serializable]
    public class TaskData
    {
        public string title;
        public string description;
    }

    private void Start()
    {
        if (tasks == null || tasks.Length == 0)
        {
            tasks = new TaskData[]
            {
                new TaskData { title = "За работу!", description = "Прочитать газету" },
                new TaskData { title = "За работу!", description = "Позвонить и устроиться\nна одну из работ" },
                new TaskData { title = "За работу!", description = "Поспать перед первой\nсменой" },
                new TaskData { title = "Первая смена", description = "Завершить смену" }
            };
        }

        taskPanel.SetActive(true);
        visiblePos = taskPanel.transform.localPosition;
        hiddenPos = visiblePos + new Vector3(panelMoveDistance, 0, 0);

        currentTaskIndex = SaveSystem.LoadTaskProgress();

        Debug.Log($"[TaskManager] Загружен индекс задачи: {currentTaskIndex}");

        ShowCurrentTask();
    }

    private void Update()
    {
        if (isPanelMoving)
        {
            moveTimer += Time.deltaTime;
            float t = Mathf.Clamp01(moveTimer / moveDuration);

            taskPanel.transform.localPosition = Vector3.Lerp(animStartPos, animTargetPos, t);

            if (t >= 1f)
            {
                isPanelMoving = false;
            }
        }
        if (Input.GetKeyDown(KeyCode.Tab) && !isPanelMoving)
        {
            if (!isPanelHidden)
            {
                HidePanel();
            }
            else
            {
                ShowPanel();
            }
        }
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
            Invoke(nameof(NextTask), 1.5f);
        }
    }

    private void NextTask()
    {
        SaveSystem.SaveTaskProgress(currentTaskIndex);
        ShowCurrentTask();
    }
}
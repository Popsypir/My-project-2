
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Фоновый счётчик игрового времени. Живёт между сценами (DontDestroyOnLoad).
/// Не имеет HUD — только считает и сохраняет время в слот.
/// </summary>
public class GameTimeTracker : MonoBehaviour
{
    public static GameTimeTracker Instance { get; private set; }

    [Header("Сцены-меню (время не считается)")]
    [SerializeField] private string[] menuSceneNames = new[] { "Menu", "MainMenu", "StartMenu" };

    private float _accumulatedSeconds;
    private bool _isTracking;

    private void Awake()
    {
        if (transform.parent != null)
            transform.SetParent(null);

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
        _isTracking = !IsMenuScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_isTracking) return;
        _accumulatedSeconds += Time.deltaTime;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isTracking = !IsMenuScene(scene.name);
    }

    private bool IsMenuScene(string name)
    {
        foreach (var menu in menuSceneNames)
        {
            if (string.Equals(menu, name, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    #region Публичные методы

    /// <summary>Сбросить счётчик (новая игра).</summary>
    public void Reset() => _accumulatedSeconds = 0f;

    /// <summary>Загрузить время из слота.</summary>
    public void LoadFromSlot(float seconds)
    {
        _accumulatedSeconds = Mathf.Max(0f, seconds);
    }

    /// <summary>Сохранить текущее время в конкретный слот.</summary>
    public void SaveToSlot(int slotIndex)
    {
        SaveSystem.SavePlayTime(slotIndex, _accumulatedSeconds);
    }

    /// <summary>Сохранить в последний активный слот.</summary>
    public void SaveToLastSlot()
    {
        if (SaveSystem.SlotCount == 0) return;
        var last = SaveSystem.GetAllSlots()[SaveSystem.SlotCount - 1];
        SaveToSlot(last.slotIndex);
    }

    public float GetSeconds() => _accumulatedSeconds;

    /// <summary>Формат ЧЧ:ММ:СС (часы не ограничены 24).</summary>
    public static string FormatTime(float totalSeconds)
    {
        if (totalSeconds < 0f) totalSeconds = 0f;
        int total = Mathf.FloorToInt(totalSeconds);
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return $"{h:D2} : {m:D2} : {s:D2}";
    }

    #endregion
}
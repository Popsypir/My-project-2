using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Дела" - список задач с двумя вкладками:
    ///  - "От игры" - задания сюжета из TaskManager (только для чтения):
    ///    выполненные отмечены галочкой и зачёркнуты, текущее - без галочки;
    ///  - "Мои" - список, который игрок ведёт сам: "+" добавляет задачу,
    ///    клик по квадратику ставит галочку и зачёркивает (задача остаётся),
    ///    клик по тексту - редактирование, свайп вправо - удалить,
    ///    свайп влево - закрепить наверху (повторный свайп влево - открепить).
    ///
    /// Свои задачи сохраняются на диск при каждом изменении (todo_list.json).
    /// Строки списка создаются копированием шаблона _rowTemplate (см. TodoItemRow).
    /// </summary>
    public class TodoApp : PhoneAppBase
    {
        [Serializable]
        public class TodoItem
        {
            public string Id;
            public string Text;
            public bool Done;
            public bool Pinned;
            // Когда закрепили - последняя закреплённая задача оказывается самой верхней
            public long PinnedAt;
        }

        [Serializable]
        private class TodoSaveData
        {
            public List<TodoItem> Items = new();
        }

        private enum Page { Game, Player }

        [Header("Вкладки")]
        [SerializeField] private Button _gameTabButton;
        [SerializeField] private Button _playerTabButton;
        [SerializeField] private GameObject _gamePage;
        [SerializeField] private GameObject _playerPage;
        [SerializeField] private Color _activeTabColor = new(1f, 0.82f, 0.35f, 1f);
        [SerializeField] private Color _inactiveTabColor = new(1f, 1f, 1f, 0.25f);

        [Header("Шапка")]
        [SerializeField] private TMP_Text _title;
        [SerializeField] private string _gameTitle = "Задания";
        [SerializeField] private string _playerTitle = "Важные дела";
        [Tooltip("\"+\" - видна только на вкладке своих задач")]
        [SerializeField] private Button _addButton;

        [Header("Списки")]
        [SerializeField] private RectTransform _gameList;
        [SerializeField] private RectTransform _playerList;
        [SerializeField] private ScrollRect _playerScroll;
        [Tooltip("Выключенный образец строки - для каждой задачи делается его копия")]
        [SerializeField] private TodoItemRow _rowTemplate;
        [Tooltip("Подсказка, когда своих задач ещё нет")]
        [SerializeField] private GameObject _playerEmptyHint;
        [Tooltip("Подсказка, когда все задания игры выполнены")]
        [SerializeField] private GameObject _gameDoneHint;

        private readonly List<TodoItem> _items = new();
        private readonly List<TodoItemRow> _playerRows = new();
        private readonly List<TodoItemRow> _gameRows = new();
        private Page _page = Page.Player;
        private bool _loaded;

        public IReadOnlyList<TodoItem> Items => _items;
        public bool Contains(TodoItem item) => _items.Contains(item);

        private static string SavePath => Path.Combine(Application.persistentDataPath, "todo_list.json");

        private void Awake()
        {
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
            if (_gameTabButton != null) _gameTabButton.onClick.AddListener(() => ShowPage(Page.Game));
            if (_playerTabButton != null) _playerTabButton.onClick.AddListener(() => ShowPage(Page.Player));
            if (_addButton != null) _addButton.onClick.AddListener(AddTask);
            EnsureLoaded();
        }

        private void OnEnable() => TaskManager.ProgressChanged += RebuildGameList;
        private void OnDisable() => TaskManager.ProgressChanged -= RebuildGameList;

        public override void OnOpen()
        {
            base.OnOpen();
            EnsureLoaded();
            RebuildGameList();
            RebuildPlayerList();
            ShowPage(_page);
        }

        public override void OnClose()
        {
            // Задача, которую как раз печатали, не должна потеряться
            foreach (var row in _playerRows)
                if (row != null) row.FinishEdit();
            base.OnClose();
        }

        private void ShowPage(Page page)
        {
            _page = page;
            bool game = page == Page.Game;
            if (_gamePage != null) _gamePage.SetActive(game);
            if (_playerPage != null) _playerPage.SetActive(!game);
            if (_addButton != null) _addButton.gameObject.SetActive(!game);
            if (_title != null) _title.text = game ? _gameTitle : _playerTitle;
            PaintTab(_gameTabButton, game);
            PaintTab(_playerTabButton, !game);
        }

        private void PaintTab(Button tab, bool active)
        {
            if (tab == null || tab.targetGraphic == null) return;
            tab.targetGraphic.color = active ? _activeTabColor : _inactiveTabColor;
        }

        // --- Свои задачи ---

        public void AddTask()
        {
            if (_page != Page.Player) ShowPage(Page.Player);

            // Пустая задача уже есть - просто возвращаемся к ней, а не плодим пустые
            var empty = _items.Find(i => string.IsNullOrWhiteSpace(i.Text));
            if (empty == null)
            {
                empty = new TodoItem { Id = Guid.NewGuid().ToString(), Text = "" };
                _items.Add(empty);
            }
            RebuildPlayerList();

            var row = _playerRows.Find(r => r.Item == empty);
            if (row != null)
            {
                if (_playerScroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    _playerScroll.verticalNormalizedPosition = empty.Pinned ? 1f : 0f;
                }
                row.BeginEdit();
            }
        }

        public void SetDone(TodoItem item, bool done)
        {
            item.Done = done;
            Save();
        }

        public void SetText(TodoItem item, string text)
        {
            text = text?.Trim() ?? "";
            if (text.Length == 0)
            {
                // Стёрли весь текст - такой задаче незачем висеть пустой строкой.
                // Если экран как раз выключается - строки перестроятся при открытии
                Remove(item, isActiveAndEnabled);
                return;
            }
            item.Text = text;
            Save();
        }

        // rebuild = false - строки перестроит позже сам вызывающий (TodoItemRow
        // сначала доигрывает анимацию свайпа, потом зовёт RefreshPlayerList)
        public void Remove(TodoItem item, bool rebuild = true)
        {
            _items.Remove(item);
            Save();
            if (rebuild) RebuildPlayerList();
        }

        public void TogglePin(TodoItem item, bool rebuild = true)
        {
            item.Pinned = !item.Pinned;
            item.PinnedAt = item.Pinned ? DateTime.UtcNow.Ticks : 0;
            Save();
            if (rebuild) RefreshPlayerList(item.Pinned);
        }

        public void RefreshPlayerList(bool scrollToTop = false)
        {
            RebuildPlayerList();
            if (scrollToTop && _playerScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _playerScroll.verticalNormalizedPosition = 1f;
            }
        }

        // Закреплённые сверху (последняя закреплённая - самая верхняя),
        // под ними остальные в том порядке, в каком их добавляли
        private List<TodoItem> SortedItems()
        {
            var pinned = _items.FindAll(i => i.Pinned);
            pinned.Sort((a, b) => b.PinnedAt.CompareTo(a.PinnedAt));
            pinned.AddRange(_items.FindAll(i => !i.Pinned));
            return pinned;
        }

        private void RebuildPlayerList()
        {
            ClearRows(_playerRows);
            if (_playerList == null || _rowTemplate == null) return;

            foreach (var item in SortedItems())
            {
                var row = CreateRow(_playerList);
                row.Bind(this, item, _playerScroll);
                _playerRows.Add(row);
            }
            if (_playerEmptyHint != null) _playerEmptyHint.SetActive(_items.Count == 0);
        }

        // --- Задания от игры ---

        private void RebuildGameList()
        {
            ClearRows(_gameRows);
            if (_gameList == null || _rowTemplate == null) return;

            var tasks = TaskManager.DefaultTasks;
            int progress = Mathf.Clamp(TaskManager.Progress, 0, tasks.Length);

            // Будущие задания не показываем - только выполненные и текущее
            int shown = Mathf.Min(progress + 1, tasks.Length);
            for (int i = 0; i < shown; i++)
            {
                var row = CreateRow(_gameList);
                string text = tasks[i].description.Replace("\n", " ").Trim();
                row.BindReadOnly(text, i < progress);
                _gameRows.Add(row);
            }
            if (_gameDoneHint != null) _gameDoneHint.SetActive(progress >= tasks.Length);
        }

        private TodoItemRow CreateRow(RectTransform parent)
        {
            var row = Instantiate(_rowTemplate, parent);
            row.gameObject.SetActive(true);
            return row;
        }

        private static void ClearRows(List<TodoItemRow> rows)
        {
            foreach (var row in rows)
                if (row != null)
                {
                    // Сначала отцепляем, чтобы раскладка сразу не учитывала строку,
                    // а сам объект удалится в конце кадра
                    row.transform.SetParent(null, false);
                    Destroy(row.gameObject);
                }
            rows.Clear();
        }

        // --- Сохранение ---

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _items.Clear();
            try
            {
                if (!File.Exists(SavePath)) return;
                var data = JsonUtility.FromJson<TodoSaveData>(File.ReadAllText(SavePath));
                if (data?.Items != null)
                    _items.AddRange(data.Items.FindAll(i => i != null && !string.IsNullOrWhiteSpace(i.Text)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TodoApp] Не удалось прочитать список дел: {e.Message}");
            }
        }

        private void Save()
        {
            try
            {
                // Пустую задачу, которую ещё печатают, на диск не пишем
                var data = new TodoSaveData { Items = _items.FindAll(i => !string.IsNullOrWhiteSpace(i.Text)) };
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TodoApp] Не удалось сохранить список дел: {e.Message}");
            }
        }
    }
}

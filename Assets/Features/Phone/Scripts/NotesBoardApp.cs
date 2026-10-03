using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    public enum BoardMode
    {
        Pan,   // таскаем саму доску, заметки можно двигать за их "шапку" как обычно
        Draw,  // рисуем от руки прямо на доске (см. BoardDrawingLayer)
        Pin,   // кликаем по двум точкам подряд (заметка или пустое место доски) -
               // соединяем их красной ниткой, по нитке - удаляем её
        Erase  // стираем нарисованное, таская курсор по доске (см. BoardDrawingLayer)
    }

    /// <summary>
    /// Приложение "Доска" - интерактивная доска расследований: заметки (можно
    /// писать и таскать), свободное рисование от руки (стрелочки, обводки) и
    /// булавки с красными нитками между заметками - как в детективных фильмах.
    ///
    /// Сама доска больше экрана телефона - таскается как обычный ScrollRect
    /// (Clamped - утащить за пределы нельзя). Режим (Pan/Draw/Pin/Erase)
    /// переключается кнопками тулбара - в режиме рисования или булавок
    /// перетаскивание самой доски отключается, чтобы не мешало.
    ///
    /// Всё содержимое доски (заметки, нитки, рисунок) сохраняется на диск при
    /// закрытии экрана и подтягивается обратно при открытии - см.
    /// SaveBoard/LoadBoard.
    /// </summary>
    public class NotesBoardApp : PhoneAppBase
    {
        [Header("Доска")]
        [SerializeField] private RectTransform _boardContent;
        [SerializeField] private ScrollRect _scrollRect;
        [Tooltip("Видимая область доски - новая заметка через кнопку \"+\" появляется по её центру")]
        [SerializeField] private RectTransform _viewport;

        [Header("Заметки")]
        [SerializeField] private StickyNoteUI _notePrefab;

        [Header("Рисование")]
        [SerializeField] private BoardDrawingLayer _drawingLayer;

        [Header("Нитки")]
        [SerializeField] private RedStringUI _stringPrefab;
        [Tooltip("Куда класть созданные нитки - обычно отдельный объект внутри доски, поверх заметок")]
        [SerializeField] private RectTransform _stringsContainer;

        [Header("Панель инструментов")]
        [SerializeField] private Button _panModeButton;
        [SerializeField] private Button _drawModeButton;
        [SerializeField] private Button _pinModeButton;
        [SerializeField] private Button _addNoteButton;
        [SerializeField] private Button _eraseModeButton;

        [Header("Булавки в любой точке доски")]
        [Tooltip("Ловит клики по пустому месту доски в режиме \"Булавка\" (см. BoardPinLayer)")]
        [SerializeField] private BoardPinLayer _pinLayer;

        [Header("Курсор")]
        [Tooltip("Показывается вместо обычного курсора, пока активен режим \"Рука\" - для понятности, что сейчас можно таскать доску")]
        [SerializeField] private Texture2D _panCursor;
        [SerializeField] private Vector2 _panCursorHotspot = new(12, 12);

        [Header("Зум (для границ после панорамирования средней кнопкой)")]
        [SerializeField] private BoardZoomController _zoomController;

        [Header("Ластик - N быстрых нажатий подряд полностью стирают рисунок")]
        [SerializeField] private int _eraseButtonClicksToFullClear = 5;
        [SerializeField] private float _eraseButtonClickWindow = 1.5f;

        public BoardMode CurrentMode { get; private set; } = BoardMode.Pan;

        // Масштаб доски (см. BoardZoomController) - используется NoteDragHandle,
        // чтобы скорость таскания заметки не "плыла" при отдалении/приближении
        public float ContentScale => _boardContent != null ? _boardContent.localScale.x : 1f;

        private Canvas _canvas;

        // Первая выбранная точка нитки - или PinAnchor заметки (тогда _pinFirstNote
        // заполнен), или отдельная "свободная" булавка прямо на доске (тогда
        // _pinFirstNote == null). _previewString тянется от неё до курсора, пока
        // не поставлена вторая точка (см. Update/BoardPinLayer)
        private RectTransform _pinFirstPoint;
        private StickyNoteUI _pinFirstNote;
        private RedStringUI _previewString;
        private RectTransform _mouseFollower;

        private readonly List<StickyNoteUI> _notes = new();
        private readonly List<RedStringUI> _strings = new();

        // Средняя кнопка мыши таскает доску в любом режиме - чтобы не приходилось
        // всё время переключаться на "Рука" ради панорамирования
        private bool _isMiddleDragging;
        private Vector2 _lastMiddleScreenPos;

        // Счётчик быстрых нажатий на кнопку "Ластик" - см. HandleEraseButtonClicked
        private int _eraseButtonClickCount;
        private float _lastEraseButtonClickTime;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void Start()
        {
            if (_panModeButton != null) _panModeButton.onClick.AddListener(() => SetMode(BoardMode.Pan));
            if (_drawModeButton != null) _drawModeButton.onClick.AddListener(() => SetMode(BoardMode.Draw));
            if (_pinModeButton != null) _pinModeButton.onClick.AddListener(() => SetMode(BoardMode.Pin));
            if (_addNoteButton != null) _addNoteButton.onClick.AddListener(CreateNoteAtViewportCenter);
            if (_eraseModeButton != null) _eraseModeButton.onClick.AddListener(HandleEraseButtonClicked);

            _pinLayer?.Initialize(this, _boardContent);

            LoadBoard();

            SetMode(BoardMode.Pan);
        }

        private void Update()
        {
            // Пока висит незавершённая нитка - тянем её свободный конец за курсором
            if (_previewString != null)
                UpdateMouseFollowerPosition();

            HandleMiddleMousePan();
        }

        // Средняя кнопка мыши таскает доску независимо от текущего инструмента -
        // не через ScrollRect (он реагирует только на левую кнопку и только в
        // режиме Pan), а напрямую двигаем Content, как и при зуме
        private void HandleMiddleMousePan()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _boardContent == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _isMiddleDragging = true;
                _lastMiddleScreenPos = mouse.position.ReadValue();
                return;
            }

            if (mouse.middleButton.wasReleasedThisFrame)
                _isMiddleDragging = false;

            if (!_isMiddleDragging) return;

            Vector2 currentScreenPos = mouse.position.ReadValue();
            Vector2 screenDelta = currentScreenPos - _lastMiddleScreenPos;
            _lastMiddleScreenPos = currentScreenPos;

            float scale = (_canvas != null ? _canvas.scaleFactor : 1f) * ContentScale;
            _boardContent.anchoredPosition += screenDelta / Mathf.Max(0.01f, scale);

            _zoomController?.ClampContentPosition();
        }

        private void OnDisable()
        {
            // Открыт другой экран телефона или доска закрылась - обычный курсор
            // не должен оставаться "ладошкой"
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            SaveBoard();
        }

        public void SetMode(BoardMode mode)
        {
            CurrentMode = mode;

            if (_scrollRect != null)
                _scrollRect.enabled = mode == BoardMode.Pan;

            if (_drawingLayer != null)
            {
                _drawingLayer.IsDrawingEnabled = mode == BoardMode.Draw || mode == BoardMode.Erase;
                _drawingLayer.IsErasing = mode == BoardMode.Erase;
            }

            if (_pinLayer != null)
                _pinLayer.enabled = mode == BoardMode.Pin;

            Cursor.SetCursor(mode == BoardMode.Pan ? _panCursor : null, _panCursorHotspot, CursorMode.Auto);

            // Выходя из режима "булавки" - сбрасываем незавершённый выбор первой заметки
            if (mode != BoardMode.Pin)
                CancelPinSelection();
        }

        // N быстрых нажатий подряд (в пределах _eraseButtonClickWindow каждое) -
        // секретный способ очистить всю доску разом (рисунок, заметки, нитки)
        private void HandleEraseButtonClicked()
        {
            SetMode(BoardMode.Erase);

            if (Time.unscaledTime - _lastEraseButtonClickTime > _eraseButtonClickWindow)
                _eraseButtonClickCount = 0;

            _lastEraseButtonClickTime = Time.unscaledTime;
            _eraseButtonClickCount++;

            if (_eraseButtonClickCount < _eraseButtonClicksToFullClear) return;

            _eraseButtonClickCount = 0;
            ClearWholeBoard();
        }

        // Полностью очищает доску: рисунок, все заметки, все нитки (вместе со
        // свободными булавками, которые принадлежат ниткам - см. RedStringUI.OnDestroy)
        public void ClearWholeBoard()
        {
            CancelPinSelection();

            foreach (RedStringUI str in _strings)
                if (str != null) Destroy(str.gameObject);
            _strings.Clear();

            foreach (StickyNoteUI note in _notes)
                if (note != null) Destroy(note.gameObject);
            _notes.Clear();

            _drawingLayer?.ClearAll();
        }

        // Пока таскаем конкретную заметку (см. NoteDragHandle) - доску саму
        // двигать не даём, даже если мы в режиме Pan, иначе поедет и то, и то сразу
        public void SetBoardScrollEnabled(bool enabled)
        {
            if (_scrollRect != null)
                _scrollRect.enabled = enabled && CurrentMode == BoardMode.Pan;
        }

        // Вешается на кнопку "+" - создаёт новую заметку по центру видимой сейчас области доски
        public void CreateNoteAtViewportCenter()
        {
            if (_boardContent == null) return;

            // _viewport.position - это мировая позиция pivot вьюпорта (у нашего он
            // не по центру, а в углу), а не видимого центра - поэтому центр берём
            // из rect.center, а не из .position напрямую
            Vector2 contentLocalPoint = _viewport != null
                ? (Vector2)_boardContent.InverseTransformPoint(_viewport.TransformPoint(_viewport.rect.center))
                : Vector2.zero;

            CreateNoteAt(ContentPointToAnchoredPosition(contentLocalPoint));
        }

        public void CreateNoteAt(Vector2 anchoredPosition)
        {
            if (_notePrefab == null || _boardContent == null) return;

            StickyNoteUI note = Instantiate(_notePrefab, _boardContent);
            note.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
            note.Initialize(this);
            _notes.Add(note);

            // Новая заметка ложится последним ребёнком доски (то есть поверх ниток) -
            // возвращаем контейнер с нитками обратно наверх, чтобы нитки были видны
            if (_stringsContainer != null)
                _stringsContainer.SetAsLastSibling();
        }

        // _boardContent.InverseTransformPoint() даёт точку в системе координат pivot'а
        // доски (у неё pivot в углу), а anchoredPosition заметки отсчитывается от её
        // точки анкоринга (у заметки якорь по центру доски) - без этой поправки на
        // сдвиг между ними заметка улетала бы далеко за пределы видимой области
        private Vector2 ContentPointToAnchoredPosition(Vector2 contentLocalPoint)
        {
            Rect rect = _boardContent.rect;
            Vector2 noteAnchor = _notePrefab != null ? _notePrefab.GetComponent<RectTransform>().anchorMin : new Vector2(0.5f, 0.5f);
            Vector2 anchorReference = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, noteAnchor.x),
                Mathf.Lerp(rect.yMin, rect.yMax, noteAnchor.y));

            return contentLocalPoint - anchorReference;
        }

        // Вешается на кнопку удаления самой заметки (см. StickyNoteUI)
        public void RemoveNote(StickyNoteUI note)
        {
            _strings.RemoveAll(s =>
            {
                if (!s.ConnectsTo(note)) return false;
                Destroy(s.gameObject);
                return true;
            });

            _notes.Remove(note);

            if (_pinFirstNote == note)
                CancelPinSelection();

            Destroy(note.gameObject);
        }

        // Вызывается StickyNoteUI, когда по заметке кликнули - реагируем, только
        // если сейчас режим Pin: первый клик выбирает заметку, второй - соединяет
        // её ниткой с первой (повторный клик по той же заметке - отменяет выбор).
        // Нитку можно тянуть и не от заметки - см. HandleBoardClickedInPinMode.
        public void HandleNoteClickedInPinMode(StickyNoteUI note)
        {
            if (CurrentMode != BoardMode.Pin || note == null) return;

            if (_pinFirstPoint == null)
            {
                BeginPin(note.PinAnchor, note);
                return;
            }

            if (_pinFirstNote == note)
            {
                CancelPinSelection();
                return;
            }

            FinishPin(note.PinAnchor, note);
        }

        // Вызывается BoardPinLayer по клику на пустое место доски в режиме Pin -
        // нитку можно протянуть в любую точку, не только между заметками: первый
        // клик ставит булавку и начинает тянуть нитку к курсору, второй - ставит
        // вторую булавку и завершает нитку
        public void HandleBoardClickedInPinMode(Vector2 contentLocalPoint)
        {
            if (CurrentMode != BoardMode.Pin) return;

            RectTransform pin = CreateFreePin(contentLocalPoint);
            if (pin == null) return;

            if (_pinFirstPoint == null)
                BeginPin(pin, null);
            else
                FinishPin(pin, null);
        }

        private void BeginPin(RectTransform point, StickyNoteUI note)
        {
            if (point == null || _stringPrefab == null || _stringsContainer == null) return;

            _pinFirstPoint = point;
            _pinFirstNote = note;
            note?.SetPinHighlighted(true);

            EnsureMouseFollower();
            UpdateMouseFollowerPosition();

            _previewString = Instantiate(_stringPrefab, _stringsContainer);
            _previewString.Connect(this, _pinFirstPoint, _mouseFollower, note, null);
        }

        private void FinishPin(RectTransform point, StickyNoteUI note)
        {
            if (point == null || _previewString == null)
            {
                CancelPinSelection();
                return;
            }

            _previewString.Connect(this, _pinFirstPoint, point, _pinFirstNote, note);
            _strings.Add(_previewString);
            _previewString = null;

            _pinFirstNote?.SetPinHighlighted(false);
            _pinFirstPoint = null;
            _pinFirstNote = null;
        }

        // Выходя из режима Pin (или отменяя выбор повторным кликом) - убираем
        // незавершённую нитку; если первая точка была свободной булавкой (не на
        // заметке), она тоже уничтожается вместе с ниткой (см. RedStringUI.OnDestroy)
        private void CancelPinSelection()
        {
            _pinFirstNote?.SetPinHighlighted(false);

            if (_previewString != null)
                Destroy(_previewString.gameObject);
            else if (_pinFirstNote == null && _pinFirstPoint != null)
                Destroy(_pinFirstPoint.gameObject);

            _previewString = null;
            _pinFirstPoint = null;
            _pinFirstNote = null;
        }

        private RectTransform CreateFreePin(Vector2 anchoredPosition, string id = null)
        {
            if (_notePrefab == null || _boardContent == null) return null;

            Transform template = _notePrefab.transform.Find("PinAnchor");
            if (template == null) return null;

            GameObject pin = Instantiate(template.gameObject, _boardContent);
            var marker = pin.AddComponent<FreePinMarker>();
            marker.Id = string.IsNullOrEmpty(id) ? System.Guid.NewGuid().ToString() : id;
            var pinRect = (RectTransform)pin.transform;
            pinRect.anchorMin = pinRect.anchorMax = new Vector2(0f, 1f);
            pinRect.anchoredPosition = anchoredPosition;

            return pinRect;
        }

        private void EnsureMouseFollower()
        {
            if (_mouseFollower != null || _boardContent == null) return;

            var go = new GameObject("PinMouseFollower", typeof(RectTransform));
            _mouseFollower = (RectTransform)go.transform;
            _mouseFollower.SetParent(_boardContent, false);
            _mouseFollower.anchorMin = _mouseFollower.anchorMax = new Vector2(0f, 1f);
            _mouseFollower.sizeDelta = Vector2.zero;
        }

        private void UpdateMouseFollowerPosition()
        {
            if (_mouseFollower == null || _boardContent == null) return;

            Vector2 screenPos = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : Vector2.zero;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_boardContent, screenPos, cam, out Vector2 local);
            _mouseFollower.anchoredPosition = local;
        }

        // Вызывается RedStringUI по клику на саму нитку - режем только в режиме
        // Pin (тем же инструментом, которым нитки создаём), чтобы случайный клик
        // по нитке в режиме "Рука" её не срезал
        public void HandleStringClicked(RedStringUI str)
        {
            if (CurrentMode != BoardMode.Pin) return;

            _strings.Remove(str);
            Destroy(str.gameObject);
        }

        // Сохранение доски - заметки, нитки (и свободные булавки, к которым они
        // привязаны) и сам рисунок переживают закрытие игры. Сохраняем при
        // закрытии экрана доски (OnDisable) и подтягиваем обратно при открытии
        // (Start/LoadBoard). Хранится в Application.persistentDataPath, отдельно
        // от сцены - поэтому не мешает обычной работе с самим проектом.
        private static string SaveJsonPath => Path.Combine(Application.persistentDataPath, "notes_board.json");
        private static string SaveDrawingPath => Path.Combine(Application.persistentDataPath, "notes_board_drawing.png");

        [System.Serializable]
        private class SavedNote
        {
            public string id;
            public float x, y;
            public string text;
        }

        [System.Serializable]
        private class SavedFreePin
        {
            public string id;
            public float x, y;
        }

        [System.Serializable]
        private class SavedString
        {
            public string pointAId;
            public string pointBId;
        }

        [System.Serializable]
        private class BoardSaveData
        {
            public List<SavedNote> notes = new();
            public List<SavedFreePin> freePins = new();
            public List<SavedString> strings = new();
        }

        private void SaveBoard()
        {
            if (_boardContent == null) return;

            var data = new BoardSaveData();

            foreach (StickyNoteUI note in _notes)
            {
                if (note == null) continue;
                Vector2 pos = ((RectTransform)note.transform).anchoredPosition;
                data.notes.Add(new SavedNote { id = note.Id, x = pos.x, y = pos.y, text = note.Text });
            }

            var savedPinIds = new HashSet<string>();
            foreach (RedStringUI str in _strings)
            {
                if (str == null) continue;

                string idA = GetPointId(str.PointA, str.NoteA);
                string idB = GetPointId(str.PointB, str.NoteB);
                if (idA == null || idB == null) continue;

                data.strings.Add(new SavedString { pointAId = idA, pointBId = idB });

                if (str.NoteA == null && savedPinIds.Add(idA))
                    data.freePins.Add(new SavedFreePin { id = idA, x = str.PointA.anchoredPosition.x, y = str.PointA.anchoredPosition.y });
                if (str.NoteB == null && savedPinIds.Add(idB))
                    data.freePins.Add(new SavedFreePin { id = idB, x = str.PointB.anchoredPosition.x, y = str.PointB.anchoredPosition.y });
            }

            File.WriteAllText(SaveJsonPath, JsonUtility.ToJson(data));

            if (_drawingLayer != null)
                File.WriteAllBytes(SaveDrawingPath, _drawingLayer.GetPngBytes());
        }

        private static string GetPointId(RectTransform point, StickyNoteUI note)
        {
            if (note != null) return note.Id;
            if (point == null) return null;
            var marker = point.GetComponent<FreePinMarker>();
            return marker != null ? marker.Id : null;
        }

        private void LoadBoard()
        {
            if (_boardContent == null || !File.Exists(SaveJsonPath)) return;

            BoardSaveData data = JsonUtility.FromJson<BoardSaveData>(File.ReadAllText(SaveJsonPath));
            if (data == null) return;

            var noteById = new Dictionary<string, StickyNoteUI>();
            foreach (SavedNote n in data.notes)
            {
                if (_notePrefab == null) break;

                StickyNoteUI note = Instantiate(_notePrefab, _boardContent);
                ((RectTransform)note.transform).anchoredPosition = new Vector2(n.x, n.y);
                note.Initialize(this, n.id);
                note.SetText(n.text);
                _notes.Add(note);
                noteById[n.id] = note;
            }

            var pinById = new Dictionary<string, RectTransform>();
            foreach (SavedFreePin p in data.freePins)
            {
                RectTransform pin = CreateFreePin(new Vector2(p.x, p.y), p.id);
                if (pin != null) pinById[p.id] = pin;
            }

            foreach (SavedString s in data.strings)
            {
                RectTransform pointA = ResolvePoint(s.pointAId, noteById, pinById, out StickyNoteUI noteA);
                RectTransform pointB = ResolvePoint(s.pointBId, noteById, pinById, out StickyNoteUI noteB);
                if (pointA == null || pointB == null || _stringPrefab == null || _stringsContainer == null) continue;

                RedStringUI str = Instantiate(_stringPrefab, _stringsContainer);
                str.Connect(this, pointA, pointB, noteA, noteB);
                _strings.Add(str);
            }

            if (_stringsContainer != null)
                _stringsContainer.SetAsLastSibling();

            if (_drawingLayer != null && File.Exists(SaveDrawingPath))
                _drawingLayer.LoadFromPngBytes(File.ReadAllBytes(SaveDrawingPath));
        }

        private static RectTransform ResolvePoint(string id, Dictionary<string, StickyNoteUI> notes, Dictionary<string, RectTransform> pins, out StickyNoteUI note)
        {
            if (notes.TryGetValue(id, out note))
                return note.PinAnchor;

            note = null;
            return pins.TryGetValue(id, out RectTransform pin) ? pin : null;
        }
    }
}

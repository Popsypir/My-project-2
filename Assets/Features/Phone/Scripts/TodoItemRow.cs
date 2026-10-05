using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Одна строка в приложении "Дела" (см. TodoApp).
    ///  - клик по квадратику - галочка и зачёркивание (задача при этом остаётся);
    ///  - клик по тексту - редактирование;
    ///  - тянуть строку вправо - удалить, влево - закрепить/открепить наверху.
    /// Потянул недалеко - строка пружинит обратно, ничего не происходит.
    /// Вертикальное движение отдаётся списку (ScrollRect), чтобы его можно было
    /// листать, ухватившись прямо за строку.
    ///
    /// Строки заданий игры - только для чтения: без свайпов и редактирования.
    /// </summary>
    public class TodoItemRow : MonoBehaviour,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IScrollHandler
    {
        [Tooltip("Двигающаяся часть строки - под ней видны подложки \"Удалить\"/\"Закрепить\"")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private Button _checkbox;
        [SerializeField] private GameObject _checkmark;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_InputField _input;
        [Tooltip("Метка закреплённой задачи")]
        [SerializeField] private GameObject _pinMark;

        [Header("Высота строки")]
        [Tooltip("Длинный текст переносится, и строка становится выше, а не мельчит шрифт")]
        [SerializeField] private LayoutElement _layout;
        [SerializeField] private float _minHeight = 64f;
        [SerializeField] private float _verticalPadding = 16f;

        [Header("Подложки под свайп")]
        [Tooltip("Видна, когда тянут вправо")]
        [SerializeField] private GameObject _deleteUnderlay;
        [Tooltip("Видна, когда тянут влево")]
        [SerializeField] private GameObject _pinUnderlay;
        [SerializeField] private TMP_Text _pinUnderlayLabel;

        [Header("Свайп")]
        [Tooltip("Какую долю ширины строки нужно протянуть, чтобы сработало")]
        [SerializeField, Range(0.1f, 0.9f)] private float _swipeThreshold = 0.35f;
        [SerializeField] private float _springSpeed = 14f;

        [Header("Цвет текста")]
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _doneTextColor = new(1f, 1f, 1f, 0.55f);

        public TodoApp.TodoItem Item { get; private set; }
        public bool IsEditing => _input != null && _input.gameObject.activeSelf;
        public bool IsReadOnly { get; private set; }

        private TodoApp _app;
        private ScrollRect _scroll;

        private enum DragMode { None, Swipe, Scroll }
        private DragMode _dragMode;
        private Vector2 _pressLocal;
        private float _offset;          // текущий сдвиг строки по X
        private float _target;          // куда строка едет сама (после отпускания)
        private bool _animating;
        private System.Action _afterAnimation;

        private bool _finishingEdit;

        private float Width => ((RectTransform)transform).rect.width;

        public void Bind(TodoApp app, TodoApp.TodoItem item, ScrollRect scroll)
        {
            _app = app;
            Item = item;
            _scroll = scroll;
            IsReadOnly = false;

            if (_checkbox != null)
            {
                _checkbox.interactable = true;
                _checkbox.onClick.RemoveAllListeners();
                _checkbox.onClick.AddListener(ToggleDone);
            }
            if (_input != null)
            {
                _input.onEndEdit.RemoveAllListeners();
                _input.onEndEdit.AddListener(_ => FinishEdit());
                _input.gameObject.SetActive(false);
            }
            if (_pinUnderlayLabel != null)
                _pinUnderlayLabel.text = item.Pinned ? "Открепить" : "Закрепить";

            Refresh(item.Text, item.Done, item.Pinned);
            SetOffset(0f);
        }

        public void BindReadOnly(string text, bool done)
        {
            Item = null;
            IsReadOnly = true;
            if (_checkbox != null)
            {
                _checkbox.onClick.RemoveAllListeners();
                // Не серим квадратик (как выключенную кнопку) - просто не реагирует
                _checkbox.transition = Selectable.Transition.None;
                _checkbox.interactable = false;
            }
            if (_input != null) _input.gameObject.SetActive(false);
            Refresh(text, done, false);
            SetOffset(0f);
        }

        private void Refresh(string text, bool done, bool pinned)
        {
            if (_checkmark != null) _checkmark.SetActive(done);
            if (_pinMark != null) _pinMark.SetActive(pinned);
            if (_label != null)
            {
                _label.gameObject.SetActive(!IsEditing);
                _label.text = text;
                _label.color = done ? _doneTextColor : _textColor;
                _label.fontStyle = done
                    ? _label.fontStyle | FontStyles.Strikethrough
                    : _label.fontStyle & ~FontStyles.Strikethrough;
                FitHeight(text);
            }
        }

        private void FitHeight(string text)
        {
            if (_layout == null || _label == null) return;
            float width = _label.rectTransform.rect.width;
            if (width <= 0f) return;
            float textHeight = _label.GetPreferredValues(text, width, 0f).y;
            _layout.preferredHeight = Mathf.Max(_minHeight, textHeight + _verticalPadding);
        }

        private void ToggleDone()
        {
            if (IsReadOnly || Item == null || _app == null) return;
            if (IsEditing) FinishEdit();
            if (Item == null) return;   // пустую задачу FinishEdit уже удалил
            _app.SetDone(Item, !Item.Done);
            Refresh(Item.Text, Item.Done, Item.Pinned);
        }

        // --- Редактирование текста ---

        public void BeginEdit()
        {
            if (IsReadOnly || _input == null || Item == null) return;
            _input.gameObject.SetActive(true);
            _input.text = Item.Text;
            if (_label != null) _label.gameObject.SetActive(false);
            _input.Select();
            _input.ActivateInputField();
            _input.MoveTextEnd(false);
        }

        public void FinishEdit()
        {
            if (!IsEditing || Item == null || _finishingEdit) return;
            // Выключение поля само шлёт onEndEdit - флаг не даёт зайти сюда второй раз
            _finishingEdit = true;
            string text = _input.text;
            _input.gameObject.SetActive(false);
            _finishingEdit = false;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _input.gameObject)
                EventSystem.current.SetSelectedGameObject(null);

            var item = Item;
            _app.SetText(item, text);   // пустой текст - задача удаляется вместе со строкой
            if (this != null && _app.Contains(item))
                Refresh(item.Text, item.Done, item.Pinned);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsReadOnly || IsEditing || _animating || Mathf.Abs(_offset) > 1f) return;
            BeginEdit();
        }

        // --- Свайпы ---

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = true;
            _scroll?.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            bool horizontal = Mathf.Abs(eventData.delta.x) >= Mathf.Abs(eventData.delta.y);
            if (horizontal && !IsReadOnly && !IsEditing && !_animating && eventData.button == PointerEventData.InputButton.Left)
            {
                _dragMode = DragMode.Swipe;
                ScreenToLocal(eventData.pressPosition, eventData.pressEventCamera, out _pressLocal);
                _pressLocal.x -= _offset;
            }
            else
            {
                _dragMode = DragMode.Scroll;
                _scroll?.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragMode == DragMode.Scroll) { _scroll?.OnDrag(eventData); return; }
            if (_dragMode != DragMode.Swipe) return;

            if (ScreenToLocal(eventData.position, eventData.pressEventCamera, out var local))
                SetOffset(Mathf.Clamp(local.x - _pressLocal.x, -Width, Width));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var mode = _dragMode;
            _dragMode = DragMode.None;
            if (mode == DragMode.Scroll) { _scroll?.OnEndDrag(eventData); return; }
            if (mode != DragMode.Swipe) return;
            ReleaseSwipe();
        }

        // Колёсико над строкой - листаем список, а не застреваем на строке
        public void OnScroll(PointerEventData eventData) => _scroll?.OnScroll(eventData);

        // Решение по свайпу: дотянул до порога - действие, иначе пружиним назад
        public void ReleaseSwipe()
        {
            // Сами данные меняем сразу, а анимация - только картинка: если
            // закрыть телефон посреди неё, действие всё равно не потеряется
            float limit = Width * _swipeThreshold;
            if (_offset >= limit)
            {
                // Вправо - строка уезжает за край, задача удалена
                _app.Remove(Item, false);
                AnimateTo(Width, () => _app.RefreshPlayerList());
            }
            else if (_offset <= -limit)
            {
                // Влево - пружиним на место и переезжаем наверх (или обратно вниз)
                bool pinned = !Item.Pinned;
                _app.TogglePin(Item, false);
                AnimateTo(0f, () => _app.RefreshPlayerList(pinned));
            }
            else
            {
                AnimateTo(0f, null);
            }
        }

        private void AnimateTo(float target, System.Action then)
        {
            _target = target;
            _afterAnimation = then;
            _animating = true;
        }

        private void Update()
        {
            if (!_animating) return;
            // unscaledDeltaTime - телефон работает и когда игра на паузе
            float k = 1f - Mathf.Exp(-_springSpeed * Time.unscaledDeltaTime);
            float next = Mathf.Lerp(_offset, _target, k);
            if (Mathf.Abs(next - _target) < 1f) next = _target;
            SetOffset(next);
            if (next == _target) FinishAnimation();
        }

        // Мгновенно доводит анимацию до конца (нужно и тестам, где кадры не идут)
        public void FinishAnimation()
        {
            if (!_animating) return;
            _animating = false;
            SetOffset(_target);
            var then = _afterAnimation;
            _afterAnimation = null;
            then?.Invoke();
        }

        private void SetOffset(float x)
        {
            _offset = x;
            if (_content != null) _content.anchoredPosition = new Vector2(x, _content.anchoredPosition.y);
            if (_deleteUnderlay != null) _deleteUnderlay.SetActive(x > 0.5f);
            if (_pinUnderlay != null) _pinUnderlay.SetActive(x < -0.5f);
        }

        public float Offset => _offset;

        private bool ScreenToLocal(Vector2 screen, Camera cam, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, cam, out local);

        private void OnDisable()
        {
            // Телефон закрыли посреди свайпа - строка не должна остаться сдвинутой.
            // Список всё равно перестроится при следующем открытии (TodoApp.OnOpen)
            _dragMode = DragMode.None;
            _animating = false;
            _afterAnimation = null;
            SetOffset(0f);
        }
    }
}

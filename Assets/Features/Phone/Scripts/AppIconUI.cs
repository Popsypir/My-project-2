using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GamePhone
{
    /// <summary>
    /// Иконка приложения на домашнем экране телефона.
    /// Размещается вручную в редакторе (не создаётся кодом), поэтому её всегда
    /// видно в Hierarchy и Scene/Game view ещё до запуска игры.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AppIconUI : MonoBehaviour
    {
        [Header("Какое приложение открывает эта иконка")]
        [Tooltip("Перетащи сюда объект с компонентом приложения (например NotesScreen с NotesApp) из Hierarchy")]
        [SerializeField] private PhoneAppBase _appSource;

        [Header("Кто переключает экраны телефона")]
        [SerializeField] private PhoneScreenManager _screenManager;

        [Header("Визуал (можно настраивать прямо в инспекторе)")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _label;

        private Button _button;

        private IPhoneApp App => _appSource;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            if (App == null)
            {
                Debug.LogWarning($"[{name}] Поле 'App Source' не назначено или объект не реализует IPhoneApp", this);
                return;
            }

            if (_screenManager == null)
            {
                Debug.LogWarning($"[{name}] Поле 'Screen Manager' не назначено", this);
                return;
            }

            _screenManager.OpenApp(App);
        }

#if UNITY_EDITOR
        // Кнопка в инспекторе (правый клик по компоненту -> это действие),
        // подтягивает иконку и название прямо из приложения, чтобы не вбивать вручную.
        [ContextMenu("Синхронизировать иконку и текст из приложения")]
        private void SyncFromAppInEditor()
        {
            if (App == null)
            {
                Debug.LogWarning("Сначала назначь поле 'App Source'", this);
                return;
            }
            if (_iconImage != null) _iconImage.sprite = App.Icon;
            if (_label != null) _label.text = App.AppName;

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

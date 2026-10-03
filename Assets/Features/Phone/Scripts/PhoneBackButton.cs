using UnityEngine;
using UnityEngine.UI;

namespace GamePhone
{
    /// <summary>
    /// Кнопка "назад", возвращающая на домашний экран из любого приложения.
    /// Можно разместить одну общую кнопку поверх всех экранов приложений
    /// (важно: должна быть НИЖЕ AppsRoot в Hierarchy, чтобы рисоваться поверх).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PhoneBackButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private PhoneScreenManager _screenManager;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (_screenManager == null)
            {
                // Если забыли перетащить в инспекторе - попробуем найти сами.
                _screenManager = FindAnyObjectByType<PhoneScreenManager>();
                if (_screenManager == null)
                    Debug.LogError($"[{name}] Не найден PhoneScreenManager в сцене. " +
                                    "Назначь поле 'Screen Manager' в инспекторе вручную.", this);
            }

            _button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            if (_screenManager == null)
            {
                Debug.LogWarning($"[{name}] Screen Manager не назначен — кнопка 'Назад' не работает.", this);
                return;
            }

            _screenManager.ShowHome();
        }
    }
}

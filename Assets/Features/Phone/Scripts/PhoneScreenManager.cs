using System.Collections.Generic;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Хранит список всех приложений телефона и переключает,
    /// какой экран сейчас видно: домашний или экран конкретного приложения.
    /// </summary>
    public class PhoneScreenManager : MonoBehaviour
    {
        [SerializeField] private GameObject _homeScreenRoot;

        [Tooltip("Перетащите сюда объекты с компонентом-приложением (например NotesApp, SettingsApp)")]
        [SerializeField] private PhoneAppBase[] _appBehaviours;

        private readonly List<IPhoneApp> _apps = new();
        private IPhoneApp _currentApp;

        public IReadOnlyList<IPhoneApp> Apps => _apps;

        private void Awake()
        {
            foreach (var app in _appBehaviours)
            {
                if (app != null)
                    _apps.Add(app);
            }

            ShowHome();
        }

        public void OpenApp(string appId)
        {
            var app = _apps.Find(a => a.AppId == appId);
            if (app == null)
            {
                Debug.LogWarning($"[PhoneScreenManager] Приложение с id '{appId}' не найдено");
                return;
            }
            OpenApp(app);
        }

        public void OpenApp(IPhoneApp app)
        {
            _currentApp?.OnClose();
            _homeScreenRoot.SetActive(false);
            _currentApp = app;
            app.OnOpen();
        }

        public void ShowHome()
        {
            _currentApp?.OnClose();
            _currentApp = null;
            _homeScreenRoot.SetActive(true);
        }
    }
}

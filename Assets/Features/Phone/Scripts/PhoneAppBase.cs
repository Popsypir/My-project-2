using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Базовая реализация IPhoneApp. Обычные приложения (Заметки, Настройки и т.д.)
    /// просто наследуются от этого класса и переопределяют OnOpen/OnClose при необходимости.
    /// </summary>
    public abstract class PhoneAppBase : MonoBehaviour, IPhoneApp
    {
        [Header("App Info")]
        [SerializeField] private string _appId;
        [SerializeField] private string _appName;
        [SerializeField] private Sprite _icon;

        [Header("Screen")]
        [Tooltip("Корневой GameObject экрана этого приложения (включается/выключается автоматически)")]
        [SerializeField] private GameObject _screenRoot;

        public string AppId => _appId;
        public string AppName => _appName;
        public Sprite Icon => _icon;
        public GameObject ScreenRoot => _screenRoot;

        public virtual void OnOpen()
        {
            if (_screenRoot != null)
                _screenRoot.SetActive(true);
        }

        public virtual void OnClose()
        {
            if (_screenRoot != null)
                _screenRoot.SetActive(false);
        }
    }
}

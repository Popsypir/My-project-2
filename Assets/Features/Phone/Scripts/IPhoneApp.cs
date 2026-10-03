using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Контракт любого "приложения" внутри телефона.
    /// Не наследуйтесь от этого интерфейса напрямую в MonoBehaviour без нужды —
    /// для обычных случаев используйте PhoneAppBase.
    /// </summary>
    public interface IPhoneApp
    {
        string AppId { get; }
        string AppName { get; }
        Sprite Icon { get; }
        GameObject ScreenRoot { get; }

        void OnOpen();
        void OnClose();
    }
}

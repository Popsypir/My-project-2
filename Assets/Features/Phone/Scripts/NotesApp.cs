using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Пример простого приложения "Заметки". Наследуется от PhoneAppBase,
    /// поэтому автоматически получает AppId/AppName/Icon и включение/выключение экрана.
    /// </summary>
    public class NotesApp : PhoneAppBase
    {
        [SerializeField] private InputField _textField;

        public override void OnOpen()
        {
            base.OnOpen();
            // Тут можно подгрузить сохранённый текст заметки, например из PlayerPrefs
        }

        public override void OnClose()
        {
            // Тут можно сохранить текст заметки перед закрытием
            base.OnClose();
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamePhone
{
    /// <summary>
    /// Проверяет, печатает ли игрок прямо сейчас в каком-нибудь текстовом поле
    /// (номер телефона, имя контакта и т.п.) - не важно, в каком именно, ничего
    /// не нужно настраивать на самих полях. Используется, чтобы не двигать
    /// персонажа по WASD, пока идёт ввод текста (иначе W/A/S/D одновременно
    /// печатались бы в поле И двигали бы персонажа).
    /// </summary>
    public static class UIFocusState
    {
        public static bool IsTypingInField()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;

            GameObject selected = events.currentSelectedGameObject;
            if (selected == null) return false;

            var tmpField = selected.GetComponent<TMP_InputField>();
            if (tmpField != null) return tmpField.isFocused;

            var uguiField = selected.GetComponent<InputField>();
            if (uguiField != null) return uguiField.isFocused;

            return false;
        }
    }
}

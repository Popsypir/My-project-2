using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Простейший фикс курсора для сцены с видом от первого лица - прячет
    /// и фиксирует курсор по центру экрана при старте. Повесь на любой объект
    /// сцены. Если позже подключишь RegisterModeController - можно удалить
    /// этот компонент, он сам будет управлять курсором (ходьба/касса).
    /// </summary>
    public class CursorLocker : MonoBehaviour
    {
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

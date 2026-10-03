using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Висит на иконке "Выход" в телефоне (часть общего Phone.prefab).
    /// Открывает диалог подтверждения (см. ExitGameApp) - тот живёт не внутри
    /// телефона, а отдельным объектом на Canvas сцены, поэтому ищется через
    /// статический Instance, а не через прямую ссылку в инспекторе (её бы
    /// пришлось перепривязывать отдельно в каждой из 5 сцен).
    /// </summary>
    public class ExitButtonHandler : MonoBehaviour
    {
        public void OpenExitDialog()
        {
            ExitGameApp.Instance?.Show();
        }
    }
}

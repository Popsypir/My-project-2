using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Пустой маркер - отличает "свободную" булавку прямо на доске (см.
    /// NotesBoardApp.CreateFreePin) от PinAnchor заметки или служебного "хвоста"
    /// нитки за курсором. Только такие булавки RedStringUI уничтожает вместе
    /// с собой, когда нитка удаляется - у остальных точек есть свой хозяин
    /// (заметка или сама доска), который ими управляет сам.
    /// </summary>
    public class FreePinMarker : MonoBehaviour
    {
        // Нужен, чтобы сохранённые нитки можно было найти обратно после
        // перезагрузки доски (см. NotesBoardApp.SaveBoard/LoadBoard)
        public string Id;
    }
}

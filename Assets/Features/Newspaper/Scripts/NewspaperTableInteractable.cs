using UnityEngine;

namespace GamePhone.World
{
    /// <summary>
    /// Вешается на стол/тумбочку в сцене. При нажатии E рядом открывает газету.
    /// </summary>
    public class NewspaperTableInteractable : InteractableBase
    {
        [SerializeField] private NewspaperReaderController _newspaperReader;
        [SerializeField] private TaskManager taskManager;

        protected override void Interact()
        {
            if (_newspaperReader == null)
            {
                Debug.LogWarning($"[{name}] Newspaper Reader не назначен", this);
                return;
            }

            taskManager.CompleteCurrentTask(0);
            _newspaperReader.Open();
        }
    }
}

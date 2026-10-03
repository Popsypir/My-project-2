using UnityEngine;
namespace GamePhone.World

{
    public class BedInteractable : InteractableBase
    {
        [SerializeField] private TaskManager taskManager;
        protected override void Interact()
        {
            if (DayManager.Instance == null)
            {
                Debug.LogWarning($"[{name}] DayManager.Instance не найден на сцене", this);
                return;
            }

            taskManager.CompleteCurrentTask(2);
            DayManager.Instance.GoToSleep();
        }
    }
}

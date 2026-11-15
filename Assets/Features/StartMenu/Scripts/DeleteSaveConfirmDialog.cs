using UnityEngine;

public class DeleteSaveConfirmDialog : MonoBehaviour
{
    [SerializeField] private CanvasGroup delSaveConfirmDialog;
    [SerializeField] private LoadGameUI LoadGameUI;

    public static DeleteSaveConfirmDialog Instance { get; private set; }
    private int slotIndex1;

    private void Awake()
    {
        Instance = this;
    }

    public void Show(int slotIndex)
    {
        if (delSaveConfirmDialog != null)
        {
            delSaveConfirmDialog.alpha = 1f;
            delSaveConfirmDialog.interactable = true;
            delSaveConfirmDialog.blocksRaycasts = true;
            slotIndex1 = slotIndex;
        }
    }
    public void Hide()
    {
        if (delSaveConfirmDialog != null)
        {
            delSaveConfirmDialog.alpha = 0f;
            delSaveConfirmDialog.interactable = false;
            delSaveConfirmDialog.blocksRaycasts = false;
        }
    }



    public void ConfirmDelete()
    {
        Hide();
        LoadGameUI.DeleteSlot(slotIndex1);
    }
}

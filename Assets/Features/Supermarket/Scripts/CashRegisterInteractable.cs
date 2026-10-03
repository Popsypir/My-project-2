using UnityEngine;

namespace GamePhone.World
{
    /// <summary>
    /// Касса. При нажатии E рядом фиксирует игрока на месте и включает
    /// перетаскивание товаров - см. GamePhone.Shop.RegisterModeController.
    /// </summary>
    public class CashRegisterInteractable : InteractableBase
    {
        [SerializeField] private GamePhone.Shop.RegisterModeController _registerMode;

        protected override void Interact()
        {
            if (_registerMode == null)
            {
                Debug.LogWarning($"[{name}] Register Mode Controller не назначен", this);
                return;
            }

            _registerMode.EnterRegister();
        }
    }
}

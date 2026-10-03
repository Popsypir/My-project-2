using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Переключает игрока между "ходит по магазину" и "работает за кассой".
    /// За кассой: ходьба и обычная камера выключены, включается перетаскивание
    /// товаров (ItemDragController) и осмотр через ShopLookController.
    /// Вход - через CashRegisterInteractable (E рядом с кассой), выход - Escape.
    /// </summary>
    public class RegisterModeController : MonoBehaviour
    {
        [Header("Ходьба (выключается за кассой)")]
        [Tooltip("Обычно это CharacterMotor игрока")]
        [SerializeField] private MonoBehaviour _characterMotor;
        [Tooltip("Обычно это FixedCameraController/FirstPersonCameraController игрока")]
        [SerializeField] private MonoBehaviour _walkCameraController;

        [Header("Касса (включается за кассой)")]
        [SerializeField] private MonoBehaviour _itemDragController;
        [SerializeField] private MonoBehaviour _shopLookController;

        [Header("Позиция за кассой")]
        [Tooltip("Необязательно - если назначено, игрок телепортируется точно сюда при входе за кассу")]
        [SerializeField] private Transform _registerStandPoint;
        [SerializeField] private Transform _player;

        [SerializeField] private KeyCode _exitKey = KeyCode.Escape;

        public bool IsAtRegister { get; private set; }

        private void Awake()
        {
            // Стартовое состояние - игрок ходит, касса выключена, курсор спрятан
            // и зафиксирован по центру экрана (как в обычном виде от первого лица)
            SetEnabled(_itemDragController, false);
            SetEnabled(_shopLookController, false);
            SetEnabled(_characterMotor, true);
            SetEnabled(_walkCameraController, true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (IsAtRegister && Input.GetKeyDown(_exitKey))
                ExitRegister();
        }

        public void EnterRegister()
        {
            if (IsAtRegister) return;
            IsAtRegister = true;

            if (_registerStandPoint != null && _player != null)
                _player.SetPositionAndRotation(_registerStandPoint.position, _registerStandPoint.rotation);

            SetEnabled(_characterMotor, false);
            SetEnabled(_walkCameraController, false);
            SetEnabled(_itemDragController, true);
            SetEnabled(_shopLookController, true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ExitRegister()
        {
            if (!IsAtRegister) return;
            IsAtRegister = false;

            SetEnabled(_itemDragController, false);
            SetEnabled(_shopLookController, false);
            SetEnabled(_characterMotor, true);
            SetEnabled(_walkCameraController, true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void SetEnabled(MonoBehaviour target, bool value)
        {
            if (target != null)
                target.enabled = value;
        }
    }
}

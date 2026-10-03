using UnityEngine;
using GamePhone;
using GamePhone.Shop;

namespace GamePhone.Movement
{
    public class FirstPersonCameraController : CameraModeControllerBase
    {
        [SerializeField] private Camera _camera;
        [Tooltip("Тело персонажа - его будем разворачивать по горизонтали")]
        [SerializeField] private Transform _bodyRoot;

        [Header("Мышь")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("Пауза (необязательно)")]
        [Tooltip("Если назначено - камера замирает, пока игрок держит товар и крутит его зажатой ПКМ (ItemDragController.IsRotatingItem)")]
        [SerializeField] private ItemDragController _itemDragController;

        private float _pitch;

        private void Reset()
        {
            _camera = GetComponentInChildren<Camera>();
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (_itemDragController != null && _itemDragController.IsRotatingItem) return;
            // То же для посылок на почте: там свой скрипт переноса, связь через
            // общий счётчик (см. HeldItemRotationState)
            if (GamePhone.Warehouse.HeldItemRotationState.AnyRequested) return;
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.IsOpen) return;
            if (CursorRequestState.AnyRequested) return;

            float sensitivity = _mouseSensitivity * GameSettings.MouseSensitivity;
            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            if (_bodyRoot != null)
                _bodyRoot.Rotate(Vector3.up * mouseX);

            float pitchSign = GameSettings.InvertLookY ? 1f : -1f;
            _pitch = Mathf.Clamp(_pitch + pitchSign * mouseY, _minPitch, _maxPitch);
            _camera.transform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        public override Vector3 GetMoveDirection(Vector2 rawInput)
        {
            Vector3 forward = _camera.transform.forward;
            Vector3 right = _camera.transform.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return forward * rawInput.y + right * rawInput.x;
        }

        public override Vector3 GetHeadLookTarget()
        {
            return _camera.transform.position + _camera.transform.forward * 5f;
        }
    }
}

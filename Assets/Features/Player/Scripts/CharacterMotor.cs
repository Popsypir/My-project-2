using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 
using GamePhone;

namespace GamePhone.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _gravity = -20f;

        [Header("Прыжок")]
        [SerializeField] private float _jumpHeight = 1.5f;

        [Header("Поворот тела")]
        [Tooltip("Поворачивать персонажа лицом в сторону движения")]
        [SerializeField] private bool _rotateTowardsMovement = true;
        [Tooltip("Скорость доворота (градусов в секунду).")]
        [SerializeField] private float _rotationSpeed = 1440f;
        [Tooltip("Смещение модели, если она смотрит не туда, куда движется персонаж (для кривых FBX).")]
        [SerializeField] private float _modelForwardOffset = 0f;

        [Header("Анимация")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _isWalkingParam = "isWalking";

        [Header("Толкание физических предметов")]
        [SerializeField] private float _pushForce = 3f;

        private CharacterController _controller;
        private CameraModeControllerBase _cameraMode;
        private float _verticalVelocity;
        private PlayerControls _controls;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            _controls = new PlayerControls();
        }

        private void OnEnable()
        {
            FindCameraMode();
            SceneManager.sceneLoaded += HandleSceneLoaded;

            _controls.Gameplay.Enable();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            _controls.Gameplay.Disable();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => FindCameraMode();

        public void FindCameraMode()
        {
            _cameraMode = FindAnyObjectByType<CameraModeControllerBase>();

            if (_cameraMode == null)
                Debug.LogWarning("[CharacterMotor] На сцене не найден ни один CameraModeControllerBase " +
                                  "(FixedCameraController или FirstPersonCameraController) — персонаж не сможет двигаться");
        }

        private void Update()
        {
            Vector3 moveDir = ReadMoveDirection();

            UpdateAnimator(moveDir);
            RotateTowardsMovement(moveDir);
            ApplyMotion(moveDir);
        }

        private Vector3 ReadMoveDirection()
        {
            if (_cameraMode == null) return Vector3.zero;

            if (UIFocusState.IsTypingInField()) return Vector3.zero;
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.IsOpen) return Vector3.zero;
            if (CursorRequestState.AnyRequested) return Vector3.zero;
            Vector2 rawInput = _controls.Gameplay.Move.ReadValue<Vector2>();

            Vector3 moveDir = _cameraMode.GetMoveDirection(rawInput);

            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            return moveDir;
        }

        private void UpdateAnimator(Vector3 moveDir)
        {
            if (_animator != null)
                _animator.SetBool(_isWalkingParam, moveDir.sqrMagnitude > 0.0001f);
        }

        private void RotateTowardsMovement(Vector3 moveDir)
        {
            if (!_rotateTowardsMovement || moveDir.sqrMagnitude <= 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up) * Quaternion.Euler(0f, _modelForwardOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
        }

        private void ApplyMotion(Vector3 moveDir)
        {
            bool grounded = _controller.isGrounded;

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            if (grounded && _controls.Gameplay.Jump.WasPressedThisFrame())
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }

            _verticalVelocity += _gravity * Time.deltaTime;

            Vector3 motion = moveDir * _moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            if (hit.moveDirection.y < -0.3f) return;

            Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            body.linearVelocity = pushDirection * _pushForce;

            body.GetComponent<PushStagger>()?.NotifyPushed();
        }
    }
}
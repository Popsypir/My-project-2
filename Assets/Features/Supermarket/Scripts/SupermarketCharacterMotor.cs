using UnityEngine;
using UnityEngine.SceneManagement;
using GamePhone;
using GamePhone.Movement;

namespace GamePhone.Shop
{
    /// <summary>
    /// Двигает персонажа в сценах магазина через CharacterController. Это
    /// ОТДЕЛЬНАЯ копия CharacterMotor, специально не используем общий скрипт -
    /// правки под магазин не должны ломать Квартирку и наоборот (так уже было:
    /// оба сценария делили один CharacterMotor и постоянно конфликтовали друг
    /// с другом при настройке поворота тела).
    ///
    /// Главное отличие от CharacterMotor: тело здесь НЕ поворачивается в
    /// сторону движения. В магазине телом полностью управляет мышь через
    /// FirstPersonCameraController (._bodyRoot.Rotate) - если тело ЕЩЁ и
    /// поворачивать к направлению движения по WASD, оно начинает крутиться
    /// при любом нажатии, кроме "вперёд" (strafe/назад конфликтует с мышью).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SupermarketCharacterMotor : MonoBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _gravity = -20f;

        [Header("Прыжок")]
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;

        [Header("Анимация")]
        [Tooltip("Необязательно. Если не назначен - берётся автоматически с этого объекта или его детей")]
        [SerializeField] private Animator _animator;
        [Tooltip("Имя bool-параметра в Animator Controller, который включает анимацию ходьбы")]
        [SerializeField] private string _isWalkingParam = "isWalking";

        [Header("Толкание физических предметов")]
        [Tooltip("CharacterController сам по себе не толкает Rigidbody - без этого игрок просто упирается в предметы, как в стену")]
        [SerializeField] private float _pushForce = 3f;

        private CharacterController _controller;
        private CameraModeControllerBase _cameraMode;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            FindCameraMode();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => FindCameraMode();

        public void FindCameraMode()
        {
            _cameraMode = FindAnyObjectByType<CameraModeControllerBase>();

            if (_cameraMode == null)
                Debug.LogWarning("[SupermarketCharacterMotor] На сцене не найден CameraModeControllerBase " +
                                  "(FirstPersonCameraController) — персонаж не сможет двигаться");
        }

        private void Update()
        {
            Vector3 moveDir = ReadMoveDirection();

            UpdateAnimator(moveDir);
            ApplyMotion(moveDir);
        }

        private Vector3 ReadMoveDirection()
        {
            if (_cameraMode == null) return Vector3.zero;
            // Игрок печатает в поле (например, номер телефона) - WASD не должен
            // одновременно двигать персонажа И вводить буквы в текст.
            if (UIFocusState.IsTypingInField()) return Vector3.zero;
            // Поверх экрана показан какой-нибудь оверлей с курсором (телефон,
            // панель итогов смены и т.п.) - двигаться в это время не должен
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.IsOpen) return Vector3.zero;
            if (CursorRequestState.AnyRequested) return Vector3.zero;

            Vector2 rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
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

        private void ApplyMotion(Vector3 moveDir)
        {
            bool grounded = _controller.isGrounded;

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f; // небольшая сила прижатия к земле

            // v = sqrt(h * -2 * g) - скорость вверх, чтобы подняться ровно на _jumpHeight при текущей гравитации.
            if (grounded && Input.GetKeyDown(_jumpKey))
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);

            _verticalVelocity += _gravity * Time.deltaTime;

            Vector3 motion = moveDir * _moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        // CharacterController кинематический и не толкает Rigidbody сам -
        // без этого метода игрок просто застревал бы перед любым физическим
        // предметом. Unity вызывает это автоматически при каждом столкновении.
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            // Не толкаем то, что ниже ног (пол и т.п.) - только то, что впереди/по бокам
            if (hit.moveDirection.y < -0.3f) return;

            Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            body.linearVelocity = pushDirection * _pushForce;

            // Если этим объектом ещё и управляет какой-то скрипт (например
            // покупатель в очереди) - просим его на секунду отпустить контроль,
            // иначе он сам тут же перезапишет скорость от толчка
            body.GetComponent<PushStagger>()?.NotifyPushed();

            // Визуальное покачивание верхней части тела - само тело не падает
            // (для этого на Rigidbody должны быть включены Freeze Rotation X/Z)
            body.GetComponent<PushWobble>()?.Push(pushDirection);
        }
    }
}

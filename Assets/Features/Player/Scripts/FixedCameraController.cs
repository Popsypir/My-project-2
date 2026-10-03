using UnityEngine;
using GamePhone;

namespace GamePhone.Movement
{
    public class FixedCameraController : CameraModeControllerBase
    {
        [SerializeField] private Camera _camera;

        [Header("Игрок")]
        [Tooltip("Необязательно. Если не назначено - ищется по тегу Player")]
        [SerializeField] private Transform _player;
        [SerializeField] private string _playerTag = "Player";
        [Tooltip("Высота точки отсчёта взгляда над ногами игрока (примерно рост персонажа)")]
        [SerializeField] private float _originHeight = 1.6f;

        [Header("Мышь (цель взгляда головы)")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _minPitch = -60f;
        [SerializeField] private float _maxPitch = 60f;
        [SerializeField] private float _minYaw = -80f;
        [SerializeField] private float _maxYaw = 80f;
        [Tooltip("На каком расстоянии перед игроком (в направлении взгляда) находится точка, в которую целится голова")]
        [SerializeField] private float _lookDistance = 5f;

        private float _yaw;
        private float _pitch;

        private void Reset()
        {
            _camera = GetComponent<Camera>();
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();

            if (_player == null)
            {
                var found = GameObject.FindGameObjectWithTag(_playerTag);
                if (found != null)
                    _player = found.transform;
            }
        }

        private void Update()
        {
            if (PhoneUIController.Instance != null && PhoneUIController.Instance.IsOpen) return;
            if (CursorRequestState.AnyRequested) return;

            float sensitivity = _mouseSensitivity * GameSettings.MouseSensitivity;
            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            float pitchSign = GameSettings.InvertLookY ? 1f : -1f;

            _yaw = Mathf.Clamp(_yaw + mouseX, _minYaw, _maxYaw);
            _pitch = Mathf.Clamp(_pitch + pitchSign * mouseY, _minPitch, _maxPitch);
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
            if (_player == null)
                return _camera.transform.position + _camera.transform.forward * _lookDistance;

            Vector3 origin = _player.position + Vector3.up * _originHeight;
            Quaternion offset = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 direction = _player.rotation * offset * Vector3.forward;

            return origin + direction * _lookDistance;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = _player != null ? _player.position + Vector3.up * _originHeight
                                              : (_camera != null ? _camera.transform.position : transform.position);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, GetHeadLookTarget());
            Gizmos.DrawWireSphere(GetHeadLookTarget(), 0.1f);
        }
    }
}

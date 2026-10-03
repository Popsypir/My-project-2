using UnityEngine;
using UnityEngine.SceneManagement;
using GamePhone;
using GamePhone.World;

namespace GamePhone.Movement
{
    public class HeadLookController : MonoBehaviour
    {
        [Tooltip("Кость головы персонажа, которую нужно поворачивать")]
        [SerializeField] private Transform _headBone;

        [Header("Ограничение поворота (от нейтрального положения)")]
        [SerializeField] private float _maxYaw = 80f;
        [SerializeField] private float _maxPitch = 60f;

        [Header("Поправка оси кости")]
        [Tooltip("Трогайте, только если голова смотрит не туда (например, назад). " +
                 "Крутите в Play Mode, чтобы совместить локальную ось 'вперёд' с лицом.")]
        [SerializeField] private Vector3 _boneForwardCorrection = Vector3.zero;

        [Tooltip("Включите, если при повороте влево/вправо кость ведет себя зеркально (бывает на кривых ригах).")]
        [SerializeField] private bool _invertYaw = true;

        private CameraModeControllerBase _cameraMode;
        private NewspaperReaderController _newspaper;

        private Quaternion _neutralLocalRotation;
        private Quaternion _frozenLocalRotation;
        private Vector3 _lastTargetPoint;
        private bool _isBlocked;

        private void Start()
        {
            if (_headBone == null)
            {
                Debug.LogWarning("[HeadLookController] Не назначена кость головы (_headBone). Скрипт отключен.", this);
                enabled = false;
                return;
            }

            _neutralLocalRotation = _headBone.localRotation;
            _frozenLocalRotation = _neutralLocalRotation;

            FindComponents();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            FindComponents();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindComponents();
        }

        private void FindComponents()
        {
            _cameraMode = FindAnyObjectByType<CameraModeControllerBase>();
            _newspaper = FindAnyObjectByType<NewspaperReaderController>();
        }

        private bool IsUiBlockingLook()
        {
            return (PhoneUIController.Instance != null && PhoneUIController.Instance.IsOpen) ||
                   (_newspaper != null && _newspaper.IsOpen) ||
                   CursorRequestState.AnyRequested;
        }

        private void LateUpdate()
        {
            if (_headBone == null) return;
            if (IsUiBlockingLook())
            {
                if (!_isBlocked)
                {
                    _frozenLocalRotation = _headBone.localRotation;
                }

                _headBone.localRotation = _frozenLocalRotation;
                _isBlocked = true;
                return;
            }

            _isBlocked = false;

            if (_cameraMode == null) return;
            Vector3 targetPoint = _cameraMode.GetHeadLookTarget();
            _lastTargetPoint = targetPoint;

            Vector3 direction = targetPoint - _headBone.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion targetWorldRot = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion parentRot = _headBone.parent != null ? _headBone.parent.rotation : Quaternion.identity;
            Quaternion rawLocalRot = Quaternion.Inverse(parentRot) * targetWorldRot;

            _headBone.localRotation = ClampToNeutral(rawLocalRot);
        }

        private Quaternion ClampToNeutral(Quaternion targetLocalRot)
        {
            Quaternion referenceRot = _neutralLocalRotation * Quaternion.Euler(_boneForwardCorrection);
            Quaternion delta = Quaternion.Inverse(referenceRot) * targetLocalRot;
            Vector3 dir = delta * Vector3.forward;

            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;

            if (_invertYaw)
                yaw = -yaw;

            pitch = Mathf.Clamp(pitch, -_maxPitch, _maxPitch);
            yaw = Mathf.Clamp(yaw, -_maxYaw, _maxYaw);

            Quaternion clampedDelta = Quaternion.Euler(pitch, yaw, 0f);
            return referenceRot * clampedDelta;
        }

        private void OnDrawGizmosSelected()
        {
            if (_headBone == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_headBone.position, _lastTargetPoint);
            Gizmos.DrawWireSphere(_lastTargetPoint, 0.1f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(_headBone.position, _headBone.forward * 0.5f);
        }
    }
}
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Визуальное покачивание при толчке - "ванька-встанька". Само тело
    /// (Rigidbody) толкать МОЖНО и физически оно не падает (для этого на
    /// Rigidbody должны быть включены Freeze Rotation X и Z), а этот
    /// компонент сверху накручивает наклон только верхней части - если
    /// Animator человекоподобный (Humanoid) - наклоняется кость Spine,
    /// иначе - вся визуальная модель целиком. После толчка наклон сам
    /// пружинисто возвращается в исходное положение.
    /// </summary>
    public class PushWobble : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [Tooltip("Если риг не человекоподобный - наклоняется этот transform целиком (по умолчанию - сам Animator)")]
        [SerializeField] private Transform _fallbackTarget;

        [Header("Пружина покачивания")]
        [SerializeField] private float _maxAngle = 25f;
        [SerializeField] private float _stiffness = 120f;
        [SerializeField] private float _damping = 8f;
        [SerializeField] private float _pushImpulse = 6f;

        private Transform _target;
        private Vector3 _leanAxis = Vector3.right;
        private float _angle;
        private float _angularVelocity;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            _target = (_animator != null && _animator.isHuman)
                ? _animator.GetBoneTransform(HumanBodyBones.Spine)
                : null;

            if (_target == null)
                _target = _fallbackTarget != null ? _fallbackTarget : (_animator != null ? _animator.transform : transform);
        }

        // Вызывается тем, кто толкает (например SupermarketCharacterMotor),
        // передаётся мировое направление толчка.
        public void Push(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;

            _leanAxis = Vector3.Cross(Vector3.up, worldDirection.normalized);
            _angularVelocity -= _pushImpulse;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            float dt = Time.deltaTime;

            float force = -_stiffness * _angle - _damping * _angularVelocity;
            _angularVelocity += force * dt;
            _angle = Mathf.Clamp(_angle + _angularVelocity * dt, -_maxAngle, _maxAngle);

            if (Mathf.Abs(_angle) > 0.01f || Mathf.Abs(_angularVelocity) > 0.01f)
                _target.Rotate(_leanAxis, _angle, Space.World);
        }
    }
}

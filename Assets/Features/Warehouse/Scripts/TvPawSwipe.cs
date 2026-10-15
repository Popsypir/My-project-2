using UnityEngine;

namespace GamePhone.World
{
    /// <summary>
    /// Замах настоящей лапой игрока по телевизору (см. TvPunch): кость лапы
    /// заносится над экраном, падает на него, задерживается и уходит обратно.
    ///
    /// Двигается та же кость, что держит посылки (см. BoxCarrier) - никакой
    /// отдельной картинки, лапа у игрока одна и та же везде.
    ///
    /// Позу ставим в LateUpdate: Animator применяет свою анимацию раньше, и
    /// поставь мы кость в обычном Update - он тут же перезаписал бы её.
    /// Доигравшись, поза кости возвращается на место, иначе лапа так и
    /// осталась бы висеть в воздухе.
    ///
    /// Анимация идёт по шагам, а не корутиной - так её можно прогнать в тестах,
    /// где кадры не идут.
    /// </summary>
    public class TvPawSwipe : MonoBehaviour
    {
        [Header("Чем бьём")]
        [Tooltip("Кость лапы игрока - та же, что назначена в BoxCarrier (Armature/Bone.003)")]
        [SerializeField] private Transform _pawBone;
        [Tooltip("Меш игрока. Не назначишь - найду сам по кости лапы")]
        [SerializeField] private SkinnedMeshRenderer _playerMesh;

        [Header("Замах")]
        [Tooltip("На сколько метров лапа заносится над точкой удара")]
        [SerializeField] private float _raiseHeight = 0.8f;
        [Tooltip("На сколько лапа отходит назад, к игроку, в верхней точке замаха")]
        [SerializeField] private float _raiseBack = 0.45f;
        [Tooltip("Насколько лапа не доходит до экрана в момент удара")]
        [SerializeField] private float _hitGap = 0.08f;

        [Header("Время")]
        [Tooltip("Сколько секунд лапа летит вниз до касания")]
        [SerializeField] private float _downDuration = 0.13f;
        [Tooltip("Сколько держится на телевизоре после удара")]
        [SerializeField] private float _holdDuration = 0.07f;
        [Tooltip("Сколько уходит обратно")]
        [SerializeField] private float _upDuration = 0.2f;

        public bool IsPlaying { get; private set; }

        /// <summary>Доля от 0 до 1 от начала замаха до удара - по ней телевизор понимает, когда плющиться.</summary>
        public float HitProgress => _downDuration <= 0f ? 1f : Mathf.Clamp01(_time / _downDuration);

        private float _time;
        private bool _hitReported;

        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private bool _restCaptured;

        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private bool _meshUpdateWasOn;

        // Позу запоминаем прямо перед замахом, а не один раз при запуске:
        // анимация простоя потихоньку шевелит кость, и поза получасовой
        // давности - это уже не то место, куда лапу надо вернуть
        private void CaptureRest()
        {
            if (_pawBone == null) return;

            _restCaptured = true;
            _restLocalPos = _pawBone.localPosition;
            _restLocalRot = _pawBone.localRotation;
        }

        /// <summary>
        /// Замахнуться и ударить в заданную точку. Куда именно бить, решает
        /// TvPunch - он выбирает случайное место на экране или на корпусе.
        /// Позиция игрока нужна, чтобы занести лапу в его сторону.
        /// </summary>
        public void Play(Vector3 viewerPosition, Vector3 hitPoint, Vector3 surfaceNormal)
        {
            if (_pawBone == null) return;

            CaptureRest();
            BeginStretch();

            // От поверхности отходим по её же нормали - так лапа одинаково
            // честно ложится и на экран, и на боковину корпуса
            Vector3 towardViewer = surfaceNormal;
            towardViewer.y = 0f;
            if (towardViewer.sqrMagnitude < 0.0001f)
            {
                towardViewer = viewerPosition - hitPoint;
                towardViewer.y = 0f;
            }
            towardViewer = towardViewer.sqrMagnitude > 0.0001f ? towardViewer.normalized : Vector3.forward;

            _toPos = hitPoint + towardViewer * _hitGap;
            _fromPos = _toPos + Vector3.up * _raiseHeight + towardViewer * _raiseBack;

            // Лапа всё время повёрнута подушечками к экрану
            _toRot = Quaternion.LookRotation(-towardViewer, Vector3.up);
            _fromRot = Quaternion.LookRotation(-towardViewer, Vector3.up) * Quaternion.Euler(-55f, 0f, 0f);

            _time = 0f;
            _hitReported = false;
            IsPlaying = true;
            Apply(0f);
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            // unscaledDeltaTime - удар доигрывается, даже если игра на паузе
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>Шаг анимации. Возвращает true ровно в тот шаг, когда лапа коснулась экрана.</summary>
        public bool Advance(float deltaTime)
        {
            if (!IsPlaying) return false;

            _time += deltaTime;

            bool justHit = !_hitReported && _time >= _downDuration;
            if (justHit) _hitReported = true;

            if (_time >= _downDuration + _holdDuration + _upDuration)
            {
                IsPlaying = false;
                RestoreRest();
                EndStretch();
                return justHit;
            }

            Apply(_time);
            return justHit;
        }

        private void Apply(float time)
        {
            if (_pawBone == null) return;

            if (time <= _downDuration)
            {
                // Разгон: сверху медленно, у самого экрана быстро
                float k = _downDuration <= 0f ? 1f : time / _downDuration;
                float e = k * k;
                _pawBone.position = Vector3.LerpUnclamped(_fromPos, _toPos, e);
                _pawBone.rotation = Quaternion.SlerpUnclamped(_fromRot, _toRot, e);
            }
            else if (time <= _downDuration + _holdDuration)
            {
                _pawBone.position = _toPos;
                _pawBone.rotation = _toRot;
            }
            else
            {
                float k = _upDuration <= 0f ? 1f : (time - _downDuration - _holdDuration) / _upDuration;
                float e = 1f - (1f - k) * (1f - k);   // уходит мягко, с замедлением
                _pawBone.position = Vector3.LerpUnclamped(_toPos, _fromPos, e);
                _pawBone.rotation = Quaternion.SlerpUnclamped(_toRot, _fromRot, e);
            }
        }

        /// <summary>Вернуть кость туда, где она была до удара.</summary>
        public void RestoreRest()
        {
            if (!_restCaptured || _pawBone == null) return;

            _pawBone.localPosition = _restLocalPos;
            _pawBone.localRotation = _restLocalRot;
        }

        // Пока лапа тянется к телевизору, вершины меша уезжают далеко за его
        // обычные границы. Unity же решает, рисовать меш или нет, именно по ним -
        // и при неудачном ракурсе игрок вместе с лапой просто исчезал бы с экрана.
        // На время замаха просим считать границы каждый кадр, это чуть дороже.
        private void BeginStretch()
        {
            if (_playerMesh == null && _pawBone != null)
                _playerMesh = _pawBone.root.GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (_playerMesh == null || IsPlaying) return;

            _meshUpdateWasOn = _playerMesh.updateWhenOffscreen;
            _playerMesh.updateWhenOffscreen = true;
        }

        private void EndStretch()
        {
            if (_playerMesh != null) _playerMesh.updateWhenOffscreen = _meshUpdateWasOn;
        }

        private void OnDisable()
        {
            // Выключили посреди замаха - лапа не должна остаться торчать
            if (IsPlaying) { IsPlaying = false; RestoreRest(); EndStretch(); }
        }
    }
}

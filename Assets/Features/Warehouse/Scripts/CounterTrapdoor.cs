using System.Collections;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Кнопка под стойкой и люк в полу перед ней. Нажал E - створка откидывается,
    /// и тот, кого сейчас обслуживают (см. PostOfficeShift.NpcAtCounter), улетает
    /// вниз вместе со своей посылкой. Следующий из очереди сразу шагает к стойке.
    ///
    /// Если у стойки никого нет, люк всё равно откроется и закроется - кнопка
    /// работает всегда, просто ронять некого.
    /// </summary>
    public class CounterTrapdoor : GamePhone.World.InteractableBase
    {
        [Header("Чья очередь")]
        [Tooltip("Смена, у которой спрашиваем, кто стоит у стойки")]
        [SerializeField] private PostOfficeShift _shift;

        [Header("Люк")]
        [Tooltip("Створка люка. Её точка отсчёта должна быть на петле, по краю: " +
                 "крутится именно она, и при пивоте в середине створка провалилась бы сама в себя")]
        [SerializeField] private Transform _hatch;
        [Tooltip("Пол люка. На время открытия выключается - иначе посетитель " +
                 "останется стоять на нём и никуда не провалится")]
        [SerializeField] private Collider _hatchCollider;
        [Tooltip("На сколько градусов откидывается створка")]
        [SerializeField] private float _openAngle = 92f;
        [Tooltip("Сколько секунд открывается")]
        [SerializeField] private float _openDuration = 0.22f;
        [Tooltip("Сколько секунд остаётся открытым")]
        [SerializeField] private float _stayOpen = 1.2f;
        [Tooltip("Сколько секунд закрывается обратно")]
        [SerializeField] private float _closeDuration = 0.5f;

        [Header("Падение")]
        [Tooltip("На сколько метров посетитель улетает вниз, прежде чем исчезнуть")]
        [SerializeField] private float _fallDepth = 9f;
        [Tooltip("Ускорение падения. 9.8 - как настоящая гравитация, больше - падает резче")]
        [SerializeField] private float _fallGravity = 14f;

        [Header("Кнопка")]
        [Tooltip("Необязательно: сама кнопка - на время нажатия вдавливается")]
        [SerializeField] private Transform _button;
        [Tooltip("На сколько метров кнопка вдавливается")]
        [SerializeField] private float _buttonPress = 0.03f;
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Щелчок кнопки")]
        [SerializeField] private AudioClip _clickClip;
        [Tooltip("Лязг створки")]
        [SerializeField] private AudioClip _hatchClip;

        /// <summary>Люк открыт (или как раз открывается-закрывается).</summary>
        public bool IsBusy { get; private set; }

        private Quaternion _hatchClosed;
        private Vector3 _buttonBase;
        private Coroutine _routine;

        protected override void Awake()
        {
            base.Awake();

            if (_hatch != null) _hatchClosed = _hatch.localRotation;
            if (_button != null) _buttonBase = _button.localPosition;
            if (_hatchCollider == null && _hatch != null) _hatchCollider = _hatch.GetComponent<Collider>();
        }

        protected override void Interact() => Press();

        /// <summary>Нажать кнопку. Пока люк не закрылся обратно, второе нажатие не считается.</summary>
        public void Press()
        {
            if (IsBusy) return;

            // Жертву запоминаем сейчас, а не когда створка откроется: за эти
            // доли секунды его могли обслужить, и провалился бы уже следующий
            PostNpc victim = _shift != null ? _shift.NpcAtCounter : null;

            IsBusy = true;
            _routine = StartCoroutine(Run(victim));
        }

        // Корутина нарочно «плоская», без вложенных: только так её можно
        // прокрутить руками из проверок (см. test_trapdoor)
        private IEnumerator Run(PostNpc victim)
        {
            PressButton(true);
            Play(_clickClip);

            if (_hatchCollider != null) _hatchCollider.enabled = false;
            Play(_hatchClip);

            float t = 0f;
            while (t < _openDuration)
            {
                t += Time.deltaTime;
                SetHatch(Mathf.Clamp01(t / _openDuration));
                yield return null;
            }
            SetHatch(1f);
            PressButton(false);
            Physics.SyncTransforms();

            // Роняем только когда пол реально ушёл из-под ног
            if (victim != null && !victim.IsFalling) victim.FallThrough(_fallDepth, _fallGravity);

            t = 0f;
            while (t < _stayOpen)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Play(_hatchClip);
            t = 0f;
            while (t < _closeDuration)
            {
                t += Time.deltaTime;
                SetHatch(1f - Mathf.Clamp01(t / _closeDuration));
                yield return null;
            }

            SetHatch(0f);
            if (_hatchCollider != null) _hatchCollider.enabled = true;
            Physics.SyncTransforms();
            IsBusy = false;
        }

        // 0 - закрыт, 1 - откинут полностью
        private void SetHatch(float k)
        {
            if (_hatch != null)
                _hatch.localRotation = _hatchClosed * Quaternion.Euler(_openAngle * k, 0f, 0f);
        }

        private void PressButton(bool down)
        {
            if (_button != null)
                _button.localPosition = _buttonBase + Vector3.down * (down ? _buttonPress : 0f);
        }

        private void Play(AudioClip clip)
        {
            if (_audioSource != null && clip != null) _audioSource.PlayOneShot(clip);
        }

        private void OnDisable()
        {
            // Выключили почту на ходу - створку обязательно вернуть: иначе люк
            // останется открытым дырой в полу, и в неё провалится сам игрок
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            SetHatch(0f);
            PressButton(false);
            if (_hatchCollider != null) _hatchCollider.enabled = true;
            IsBusy = false;
        }
    }
}

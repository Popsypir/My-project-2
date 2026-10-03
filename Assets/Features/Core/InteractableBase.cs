using UnityEngine;

namespace GamePhone.World
{
    /// <summary>
    /// Базовый класс любого объекта в мире, с которым можно взаимодействовать
    /// подойдя вплотную и нажав клавишу (по умолчанию E). Наследуйтесь от него
    /// и переопределите Interact() — так сделаны NewspaperTableInteractable и BedInteractable.
    ///
    /// Требования: на объекте должен быть Collider с включённым Is Trigger,
    /// а у игрока — тег "Player" (или другой, указанный в _playerTag) и любой Collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class InteractableBase : MonoBehaviour
    {
        [Header("Взаимодействие")]
        [SerializeField] protected string _playerTag = "Player";
        [SerializeField] protected KeyCode _interactKey = KeyCode.E;

        [Tooltip("Необязательно: объект-подсказка (например текст 'Нажать E'), который включается рядом с игроком")]
        [SerializeField] protected GameObject _promptRoot;

        protected bool PlayerInRange { get; private set; }

        protected virtual void Awake()
        {
            SetPrompt(false);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            PlayerInRange = true;
            SetPrompt(true);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            PlayerInRange = false;
            SetPrompt(false);
        }

        protected virtual void Update()
        {
            if (PlayerInRange && Input.GetKeyDown(_interactKey))
                Interact();
        }

        protected virtual void SetPrompt(bool show)
        {
            if (_promptRoot != null)
                _promptRoot.SetActive(show);
        }

        protected abstract void Interact();
    }
}

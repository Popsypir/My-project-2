using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Показывает курсор, пока открыт телефон, и прячет/блокирует его обратно,
    /// когда телефон закрыт.
    ///
    /// Состояние телефона проверяется каждый кадр (а не через подписку на
    /// событие) - подписка в OnEnable оказалась ненадёжной: порядок
    /// Awake/OnEnable между этим объектом и PhoneUIController (это два разных
    /// объекта в сцене) не гарантирован, и PhoneUIController.Instance иногда
    /// ещё не существовал в момент подписки - телефон открывался/закрывался,
    /// а курсор оставался нетронутым. Проверка каждый кадр от этой гонки не
    /// зависит вообще.
    /// </summary>
    public class PhoneCursorController : MonoBehaviour
    {
        public static PhoneCursorController Instance { get; private set; }

        private bool? _lastKnownOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (PhoneUIController.Instance == null) return;

            // Курсор должен быть свободен и пока открыт сам телефон, и пока поверх
            // экрана показан какой-нибудь оверлей вроде паузы мини-игры (см.
            // CursorRequestState) - даже если в этот момент телефон уже закрыт.
            bool isOpen = PhoneUIController.Instance.IsOpen || CursorRequestState.AnyRequested;
            if (_lastKnownOpen.HasValue && _lastKnownOpen.Value == isOpen) return;

            _lastKnownOpen = isOpen;

            if (isOpen)
                HandleOpened();
            else
                HandleClosed();
        }

        private void HandleOpened()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleClosed()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

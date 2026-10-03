using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Постепенно увеличивает, сколько воров разрешено "работать" в магазине
    /// ОДНОВРЕМЕННО за смену - в начале только один, дальше по умолчанию
    /// удваивается каждые _stageDuration секунд (1, 2, 4, 8, 16...), пока
    /// охранник физически не будет успевать ловить всех.
    ///
    /// CustomerShopper перед тем, как решить, что очередной покупатель ворует,
    /// спрашивает CanBecomeThief() - если сейчас воров уже столько, сколько
    /// разрешено на этой стадии, покупатель просто идёт в очередь как честный.
    /// CustomerThief сам регистрирует начало/конец воровства (см. MarkAsThief/
    /// OnDestroy) - учитывается независимо от того, поймали вора, он сбежал
    /// или ещё идёт к выходу.
    /// </summary>
    public class TheftDifficultyManager : MonoBehaviour
    {
        public static TheftDifficultyManager Instance { get; private set; }

        [Tooltip("Сколько воров разрешено одновременно в самом начале смены")]
        [SerializeField] private int _startingCap = 1;
        [Tooltip("Через сколько секунд разрешённое число удваивается")]
        [SerializeField] private float _stageDuration = 30f;

        private float _shiftStartTime = -1f;
        private int _activeThieves;

        // Сколько воров сейчас разрешено одновременно, с учётом того, сколько
        // времени прошло с начала смены (см. ResetForNewShift) - потолка
        // намеренно нет, растёт по-настоящему бесконечно (1, 2, 4, 8, 16...),
        // ограничение по показателю степени - чисто техническое, чтобы (1 << stage)
        // не переполнился и не улетел в отрицательные числа на очень долгой смене.
        public int CurrentCap
        {
            get
            {
                if (_shiftStartTime < 0f) return _startingCap;

                int stage = Mathf.FloorToInt((Time.time - _shiftStartTime) / Mathf.Max(1f, _stageDuration));
                long cap = (long)_startingCap << Mathf.Min(stage, 30);
                return (int)Mathf.Min(cap, int.MaxValue);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Вызывается CheckoutManager при старте смены - отсчёт стадий начинается заново
        public void ResetForNewShift()
        {
            _shiftStartTime = Time.time;
            _activeThieves = 0;
        }

        public bool CanBecomeThief() => _activeThieves < CurrentCap;

        // Вызывается CustomerShopper, когда покупатель только что решил воровать
        public void RegisterThiefStarted() => _activeThieves++;

        // Вызывается CustomerThief.OnDestroy - вор пропал из магазина (поймали
        // и вышвырнули, или сам сбежал) - освобождает место для следующего
        public void RegisterThiefEnded() => _activeThieves = Mathf.Max(0, _activeThieves - 1);
    }
}

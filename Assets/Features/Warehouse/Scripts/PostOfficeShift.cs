using System.Collections;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Смена на почте. Пока идёт день, к стойке приходят посетители (см. PostNpc):
    /// одни приносят посылки, другие приходят их забирать. Игрок разносит коробки
    /// между стойкой, стеллажами и посетителями.
    ///
    /// Под вечер приезжает фургон (см. Van): всё, что игрок успел в него загрузить,
    /// он увозит - сколько влезло, столько и увезёт. Остальные коробки просто
    /// остаются лежать на почте.
    ///
    /// Длительность смены и частота посетителей настраиваются прямо в инспекторе.
    /// </summary>
    public class PostOfficeShift : MonoBehaviour
    {
        [Header("Кого спавнить")]
        [Tooltip("Модели посетителей - при каждом приходе выбирается случайная")]
        [SerializeField] private GameObject[] _npcPrefabs;
        [Tooltip("Префаб посылки - её приносят Sender-посетители")]
        [SerializeField] private GameObject _boxPrefab;
        [Tooltip("Шрифт реплик над головой посетителей. Должен поддерживать кириллицу - " +
                 "у стандартного шрифта TMP русских букв нет, вместо них будут квадраты")]
        [SerializeField] private TMPro.TMP_FontAsset _npcFont;

        [Header("Точки маршрута")]
        [Tooltip("Где посетитель появляется (снаружи, у входа)")]
        [SerializeField] private Transform _spawnPoint;
        [Tooltip("Куда он идёт - первое место перед стойкой")]
        [SerializeField] private Transform _counterPoint;
        [Tooltip("Шаг очереди: куда вставать второму, третьему и так далее - " +
                 "отсчитывается от места у стойки в его локальных координатах")]
        [SerializeField] private Vector3 _queueStep = new Vector3(0f, 0f, -1.6f);
        [Tooltip("Куда уходит - обычно та же точка, что и спавн")]
        [SerializeField] private Transform _exitPoint;

        [Header("Расписание")]
        [Tooltip("Сколько секунд длится рабочий день до приезда фургона")]
        [SerializeField] private float _shiftDuration = 300f;
        [SerializeField] private float _minSpawnInterval = 8f;
        [SerializeField] private float _maxSpawnInterval = 16f;
        [Tooltip("Сколько посетителей может находиться на почте одновременно")]
        [SerializeField] private int _maxNpcAtOnce = 3;
        [Tooltip("Доля посетителей, которые ПРИНОСЯТ посылку (остальные приходят забирать). " +
                 "0.6 значит примерно 6 из 10 приносят")]
        [Range(0f, 1f)]
        [SerializeField] private float _senderChance = 0.6f;
        [Tooltip("Первый посетитель всегда Sender - иначе в самом начале смены забирать нечего")]
        [SerializeField] private bool _firstIsAlwaysSender = true;

        [Header("Фургон")]
        [SerializeField] private Van _van;
        [Tooltip("Фургон стоит у ворот весь день или приезжает только под вечер")]
        [SerializeField] private bool _vanArrivesAtEnd = true;

        public int ParcelsDelivered { get; private set; }
        public int ReceiversServed { get; private set; }
        public int ReceiversGaveUp { get; private set; }
        public int ParcelsShipped { get; private set; }
        public bool IsShiftOver { get; private set; }
        public float TimeLeft => Mathf.Max(0f, _shiftEndTime - Time.time);

        private int _activeNpc;
        private int _spawnedTotal;
        private float _shiftEndTime;

        private void Start()
        {
            if (_vanArrivesAtEnd && _van != null)
                _van.gameObject.SetActive(false);

            _shiftEndTime = Time.time + _shiftDuration;
            StartCoroutine(SpawnLoop());
            StartCoroutine(ShiftTimer());
        }

        private IEnumerator SpawnLoop()
        {
            while (!IsShiftOver)
            {
                yield return new WaitForSeconds(Random.Range(_minSpawnInterval, _maxSpawnInterval));
                if (IsShiftOver) break;
                if (_activeNpc >= _maxNpcAtOnce) continue;

                SpawnNpc();
            }
        }

        private IEnumerator ShiftTimer()
        {
            yield return new WaitForSeconds(_shiftDuration);
            EndShift();
        }

        private void SpawnNpc()
        {
            if (_npcPrefabs == null || _npcPrefabs.Length == 0 || _spawnPoint == null)
            {
                Debug.LogWarning("[PostOfficeShift] Не назначены Npc Prefabs или Spawn Point", this);
                return;
            }

            GameObject prefab = _npcPrefabs[Random.Range(0, _npcPrefabs.Length)];
            if (prefab == null) return;

            bool sender = _firstIsAlwaysSender && _spawnedTotal == 0
                ? true
                : Random.value < _senderChance;

            // Посылку с нужным именем ещё не приняли - значит, забирать нечего,
            // и такой посетитель только зря постоит. Вместо него приходит ещё один
            // с посылкой.
            string requested = sender ? null : PickAvailableName();
            if (!sender && requested == null) sender = true;

            GameObject go = Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation);
            var npc = go.AddComponent<PostNpc>();
            npc.SetFont(_npcFont);

            Box carried = null;
            if (sender && _boxPrefab != null)
            {
                var boxGo = Instantiate(_boxPrefab, _spawnPoint.position, Quaternion.identity);
                carried = boxGo.GetComponent<Box>();
            }

            _activeNpc++;
            _spawnedTotal++;
            npc.Begin(this, sender ? PostNpc.NpcKind.Sender : PostNpc.NpcKind.Receiver,
                      _counterPoint, _exitPoint != null ? _exitPoint : _spawnPoint, carried, requested);
        }

        // Имя с бирки одной из посылок, которые сейчас лежат на почте: не в руках
        // у игрока или другого посетителя и не уехали в фургоне. Null - таких нет.
        private string PickAvailableName()
        {
            var free = new System.Collections.Generic.List<string>();
            foreach (var box in FindObjectsByType<Box>(FindObjectsSortMode.None))
                if (box != null && !box.IsHeld && !box.IsInVan && !string.IsNullOrEmpty(box.RecipientName))
                    free.Add(box.RecipientName);

            return free.Count == 0 ? null : free[Random.Range(0, free.Count)];
        }

        /// <summary>
        /// Есть ли ещё на почте посылка с таким именем. В руках у игрока - считается
        /// (он несёт её как раз этому посетителю), уехавшая в фургоне - нет.
        ///
        /// Нужно, чтобы очередь не вставала намертво: если посылку увезли, отдать
        /// её уже нечем, и ждущий у стойки иначе держал бы всех позади себя вечно.
        /// </summary>
        public bool HasParcelFor(string recipientName)
        {
            if (string.IsNullOrEmpty(recipientName)) return true;

            foreach (var box in FindObjectsByType<Box>(FindObjectsSortMode.None))
                if (box != null && box.isActiveAndEnabled && !box.IsInVan
                    && box.RecipientName == recipientName)
                    return true;

            return false;
        }

        // ---- Очередь ----
        // Посетители обслуживаются по одному: первый стоит у стойки, остальные
        // ждут позади. Без этого они шли в одну и ту же точку, толкались и
        // налезали друг на друга.
        private readonly System.Collections.Generic.List<PostNpc> _queue = new System.Collections.Generic.List<PostNpc>();

        /// <summary>Место в очереди по номеру: 0 - у самой стойки, дальше назад.</summary>
        public Vector3 QueueSlotPosition(int index) =>
            _counterPoint.position + _counterPoint.TransformVector(_queueStep) * index;

        public void JoinQueue(PostNpc npc)
        {
            PurgeQueue();
            if (npc != null && !_queue.Contains(npc)) _queue.Add(npc);
        }

        public void LeaveQueue(PostNpc npc)
        {
            bool removed = npc != null && _queue.Remove(npc);
            int before = _queue.Count;
            PurgeQueue();
            if (!removed && _queue.Count == before) return;

            // все, кто стоял позади, сдвигаются на шаг вперёд
            for (int i = 0; i < _queue.Count; i++)
                _queue[i].MoveToQueueSlot(i);
        }

        // Уничтоженный посетитель (конец смены, перезапуск) оставил бы в списке
        // "призрака": очередь росла бы назад, а первым в ней навсегда числился бы
        // тот, кого уже нет - и никого больше не обслужили бы.
        private void PurgeQueue() => _queue.RemoveAll(n => n == null);

        /// <summary>Номер в очереди: 0 - сейчас его обслуживают.</summary>
        public int QueueIndexOf(PostNpc npc) => _queue.IndexOf(npc);

        /// <summary>Обслуживают только того, кто стоит первым.</summary>
        public bool IsAtCounter(PostNpc npc)
        {
            PurgeQueue();
            return npc != null && _queue.Count > 0 && _queue[0] == npc;
        }

        public void NotifyParcelDelivered() => ParcelsDelivered++;
        public void NotifyReceiverServed() => ReceiversServed++;
        public void NotifyReceiverGaveUp() => ReceiversGaveUp++;
        public void NotifyNpcGone() => _activeNpc = Mathf.Max(0, _activeNpc - 1);

        public void EndShift()
        {
            if (IsShiftOver) return;
            IsShiftOver = true;

            if (_van != null && _vanArrivesAtEnd)
                _van.gameObject.SetActive(true);

            Debug.Log($"[Почта] Смена окончена. Принято посылок: {ParcelsDelivered}, " +
                      $"выдано: {ReceiversServed}, ушли ни с чем: {ReceiversGaveUp}");
        }

        /// <summary>Фургон уезжает - увозит всё, что в него влезло.</summary>
        public void SendVanAway()
        {
            if (_van == null) return;
            ParcelsShipped = _van.Depart();
            Debug.Log($"[Почта] Фургон увёз посылок: {ParcelsShipped}");
        }
    }
}

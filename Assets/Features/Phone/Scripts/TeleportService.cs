using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamePhone
{
    /// <summary>
    /// Телепортирует игрока по Id точки. Переживает смену сцен
    /// (DontDestroyOnLoad) - нужно для телепортации в ДРУГУЮ сцену (см.
    /// PhoneNumberEntry.SceneName, используется звонком на работу через
    /// CallApp/DayManager): объект должен дождаться конца асинхронной
    /// загрузки новой сцены и только потом завершить перемещение - если бы
    /// он уничтожался вместе со старой сценой, _pendingDestinationId
    /// терялся бы, и переход в другую сцену молча не срабатывал. У каждой
    /// сцены в иерархии может быть свой объект с этим компонентом - тогда
    /// первый загруженный побеждает (см. Instance ниже), остальные при
    /// загрузке своей сцены сами себя уничтожают.
    /// </summary>
    public class TeleportService : MonoBehaviour
    {
        public static TeleportService Instance { get; private set; }

        [Tooltip("Необязательно. Если не задано - игрок ищется по тегу 'Player' автоматически после каждой загрузки сцены")]
        [SerializeField] private Transform _player;

        [Tooltip("Тег, по которому искать игрока, если поле Player выше не назначено")]
        [SerializeField] private string _playerTag = "Player";

        private readonly Dictionary<string, TeleportDestination> _destinations = new();
        private string _pendingDestinationId;

        public event Action<TeleportDestination> OnPlayerTeleported;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            CollectDestinationsInActiveScene();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CollectDestinationsInActiveScene();

            if (_pendingDestinationId != null)
            {
                string id = _pendingDestinationId;
                _pendingDestinationId = null;
                MoveToDestination(id, out string failReason);
                if (failReason != null)
                    Debug.LogWarning($"[TeleportService] После загрузки сцены '{scene.name}': {failReason}");
            }
        }

        private void CollectDestinationsInActiveScene()
        {
            _destinations.Clear();

            foreach (var destination in FindObjectsByType<TeleportDestination>(FindObjectsSortMode.None))
                Register(destination);

            if (_destinations.Count > 0)
                Debug.Log($"[TeleportService] Точки в сцене '{SceneManager.GetActiveScene().name}': {string.Join(", ", _destinations.Keys)}");
        }

        public void Register(TeleportDestination destination)
        {
            if (destination == null || string.IsNullOrWhiteSpace(destination.DestinationId))
                return;

            string id = destination.DestinationId.Trim();

            if (_destinations.ContainsKey(id))
            {
                Debug.LogWarning($"[TeleportService] Дублирующийся Destination Id '{id}' на {destination.name}", destination);
                return;
            }

            _destinations[id] = destination;
        }

        /// <summary>
        /// Главный метод для звонилки. Сам решает, нужно ли грузить другую сцену.
        /// Если да - возвращает true сразу (сцена грузится в фоне), а сама
        /// телепортация довершится, когда сцена догрузится.
        /// </summary>
        public bool TryTeleport(PhoneNumberEntry entry, out string failReason)
        {
            failReason = null;

            if (entry == null)
            {
                failReason = "не задан номер";
                return false;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            bool needsSceneLoad = !string.IsNullOrWhiteSpace(entry.SceneName) && entry.SceneName != currentScene;

            if (needsSceneLoad)
            {
                _pendingDestinationId = entry.DestinationId;
                StartCoroutine(LoadSceneThenTeleport(entry.SceneName));
                return true;
            }

            return MoveToDestination(entry.DestinationId, out failReason);
        }

        private IEnumerator LoadSceneThenTeleport(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (op == null)
            {
                Debug.LogError($"[TeleportService] Не удалось начать загрузку сцены '{sceneName}'. " +
                                "Проверь, что сцена добавлена в Build Settings (File -> Build Profiles -> Scene List).");
                _pendingDestinationId = null;
                yield break;
            }

            yield return op;
            // Дальше телепорт довершится сам в HandleSceneLoaded, когда сцена реально загрузится
        }

        private bool MoveToDestination(string destinationId, out string failReason)
        {
            failReason = null;
            string id = destinationId?.Trim() ?? "";

            if (!_destinations.TryGetValue(id, out var destination))
            {
                failReason = $"точка с Id '{id}' не найдена в текущей сцене";
                Debug.LogWarning($"[TeleportService] {failReason}");
                return false;
            }

            var player = GetPlayer();
            if (player == null)
            {
                failReason = $"не найден игрок (ни в поле Player, ни по тегу '{_playerTag}')";
                Debug.LogWarning($"[TeleportService] {failReason}", this);
                return false;
            }

            // У игрока обычно есть включённый CharacterController - если менять
            // transform.position прямо на нём, встроенная коллизия персонажа
            // может "не заметить" перемещение и в тот же кадр вернуть игрока
            // обратно на старое место. Поэтому на время переноса выключаем его.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            player.position = destination.transform.position;
            player.rotation = destination.transform.rotation;

            if (controller != null) controller.enabled = true;

            OnPlayerTeleported?.Invoke(destination);
            return true;
        }

        private Transform GetPlayer()
        {
            // Unity-объекты сравниваются с null особым образом: если _player был
            // уничтожен при смене сцены, это условие всё равно сработает правильно.
            if (_player != null)
                return _player;

            var found = GameObject.FindGameObjectWithTag(_playerTag);
            return found != null ? found.transform : null;
        }
    }
}

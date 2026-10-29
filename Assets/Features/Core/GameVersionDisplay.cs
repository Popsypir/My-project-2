using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System;

namespace GamePhone
{
    /// <summary>
    /// Выводит версию самой игры (Application.version - берётся из Project Settings
    /// -> Player -> Version, а не версию редактора Unity) в текстовое поле.
    /// Сравнивает с последним коммитом в ветке main на GitHub и меняет цвет.
    /// При наведении показывает tooltip с объяснением цвета.
    /// </summary>
    public class GameVersionDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Версия")]
        [SerializeField] private TMP_Text _versionText;
        [Tooltip("{0} заменится на саму версию, например \"v{0}\"")]
        [SerializeField] private string _format = "ver {0}";

        [Header("Tooltip")]
        [SerializeField] private TMP_Text _tooltipText;
        [SerializeField] private CanvasGroup _tooltipCanvasGroup;
        [SerializeField] private float _fadeSpeed = 5f;

        [Header("GitHub")]
        [SerializeField] private string _githubOwner = "Popsypir";
        [SerializeField] private string _githubRepo = "My-project-2";
        [SerializeField] private string _githubToken = "";

        private string _tooltipMessage = "";
        private Color _targetColor = Color.white;
        private float _targetAlpha = 0f;
        private bool _isHovering = false;

        private void Start()
        {
            if (_versionText != null)
                _versionText.text = string.Format(_format, Application.version);

            if (_tooltipCanvasGroup != null)
            {
                _tooltipCanvasGroup.alpha = 0f;
                _tooltipCanvasGroup.interactable = false;
                _tooltipCanvasGroup.blocksRaycasts = false;
            }

            CheckVersionAsync();
        }

        private void Update()
        {
            // Плавное изменение цвета версии
            if (_versionText != null)
            {
                _versionText.color = Color.Lerp(_versionText.color, _targetColor, Time.deltaTime * _fadeSpeed);
            }

            // Плавное появление/исчезновение tooltip
            if (_tooltipCanvasGroup != null)
            {
                float currentAlpha = _tooltipCanvasGroup.alpha;
                float targetAlpha = _isHovering && !string.IsNullOrEmpty(_tooltipMessage) ? 1f : 0f;
                _tooltipCanvasGroup.alpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * _fadeSpeed);
            }
        }

        private async void CheckVersionAsync()
        {
            try
            {
                string commitMessage = await GetLatestMainCommitMessage();

                if (string.IsNullOrEmpty(commitMessage))
                {
                    SetVersionStatus(new Color(0.7f, 0.7f, 0.7f), "Неизвестно");
                    return;
                }

                string extractedVersion = ExtractVersionFromText(commitMessage);

                if (string.IsNullOrEmpty(extractedVersion))
                {
                    SetVersionStatus(new Color(0.7f, 0.7f, 0.7f), "Неизвестно");
                    return;
                }

                string localVersion = Application.version;

                if (!ParsedVersion.TryParse(localVersion, out ParsedVersion localVer))
                {
                    SetVersionStatus(new Color(0.7f, 0.7f, 0.7f), "Неизвестно");
                    return;
                }

                if (!ParsedVersion.TryParse(extractedVersion, out ParsedVersion mainVer))
                {
                    SetVersionStatus(new Color(0.7f, 0.7f, 0.7f), "Неизвестно");
                    return;
                }

                int comparison = localVer.CompareTo(mainVer);

                if (comparison == 0)
                {
                    SetVersionStatus(new Color(0.6f, 1.0f, 0.6f), "Версия актуальна");
                }
                else if (comparison < 0)
                {
                    SetVersionStatus(new Color(1.0f, 0.5f, 0.5f), "Версия устарела!");
                }
                else
                {
                    SetVersionStatus(new Color(1.0f, 0.75f, 0.5f), "В разработке");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка проверки версии: {ex.Message}");
                SetVersionStatus(new Color(0.7f, 0.7f, 0.7f), "Неизвестно");
            }
        }

        private void SetVersionStatus(Color color, string tooltip)
        {
            _targetColor = color;
            _tooltipMessage = tooltip;

            if (_tooltipText != null)
            {
                _tooltipText.text = tooltip;
            }
        }

        private string ExtractVersionFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // 1. Ищем Dev формат: 0.0.4d.3 (самый специфичный)
            var matchDev = Regex.Match(text, @"\b(\d+\.\d+\.\d+[a-zA-Z]\.\d+)\b");
            if (matchDev.Success)
            {
                return matchDev.Groups[1].Value;
            }

            // 2. Ищем Main с именем: 0.0.4-Helios
            var matchMainNamed = Regex.Match(text, @"\b(\d+\.\d+\.\d+-[a-zA-Z]+)\b");
            if (matchMainNamed.Success)
            {
                return matchMainNamed.Groups[1].Value;
            }

            // 3. Ищем Main без имени: 0.0.3 (просто три цифры)
            var matchMainSimple = Regex.Match(text, @"\b(\d+\.\d+\.\d+)\b(?![a-zA-Z])");
            if (matchMainSimple.Success)
            {
                return matchMainSimple.Groups[1].Value;
            }

            return null;
        }

        private async Task<string> GetLatestMainCommitMessage()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Unity-Version-Checker");
                if (!string.IsNullOrEmpty(_githubToken))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"token {_githubToken}");
                }

                string url = $"https://api.github.com/repos/{_githubOwner}/{_githubRepo}/commits/main";
                string jsonResponse = await client.GetStringAsync(url);

                var match = Regex.Match(jsonResponse, "\"message\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                if (match.Success)
                {
                    string message = match.Groups[1].Value;
                    message = message.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
                    return message;
                }
                return null;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
        }

        #region Version Parsing

        private class ParsedVersion : IComparable<ParsedVersion>
        {
            public int Major;
            public int Minor;
            public int Patch;
            public bool IsMain;
            public string DeityName;
            public char DevLetter;
            public int MicroUpdate;

            public static bool TryParse(string input, out ParsedVersion version)
            {
                version = new ParsedVersion();

                // Формат Main с именем: 0.0.4-Helios
                var matchMainNamed = Regex.Match(input, @"^(\d+)\.(\d+)\.(\d+)-([a-zA-Z]+)$");
                if (matchMainNamed.Success)
                {
                    version.Major = int.Parse(matchMainNamed.Groups[1].Value);
                    version.Minor = int.Parse(matchMainNamed.Groups[2].Value);
                    version.Patch = int.Parse(matchMainNamed.Groups[3].Value);
                    version.DeityName = matchMainNamed.Groups[4].Value;
                    version.IsMain = true;
                    return true;
                }

                // Формат Main без имени: 0.0.3
                var matchMainSimple = Regex.Match(input, @"^(\d+)\.(\d+)\.(\d+)$");
                if (matchMainSimple.Success)
                {
                    version.Major = int.Parse(matchMainSimple.Groups[1].Value);
                    version.Minor = int.Parse(matchMainSimple.Groups[2].Value);
                    version.Patch = int.Parse(matchMainSimple.Groups[3].Value);
                    version.DeityName = null;
                    version.IsMain = true;
                    return true;
                }

                // Формат Dev: 0.0.4d или 0.0.4d.3
                var matchDev = Regex.Match(input, @"^(\d+)\.(\d+)\.(\d+)([a-zA-Z])(?:\.(\d+))?$");
                if (matchDev.Success)
                {
                    version.Major = int.Parse(matchDev.Groups[1].Value);
                    version.Minor = int.Parse(matchDev.Groups[2].Value);
                    version.Patch = int.Parse(matchDev.Groups[3].Value);
                    version.DevLetter = matchDev.Groups[4].Value[0];

                    // Если микро-обновление не указано (например, "0.0.4d"), считаем его равным 0
                    version.MicroUpdate = string.IsNullOrEmpty(matchDev.Groups[5].Value)
                        ? 0
                        : int.Parse(matchDev.Groups[5].Value);

                    version.IsMain = false;
                    return true;
                }

                return false;
            }

            public int CompareTo(ParsedVersion other)
            {
                if (other == null) return 1;

                if (Major != other.Major) return Major.CompareTo(other.Major);
                if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
                if (Patch != other.Patch) return Patch.CompareTo(other.Patch);

                if (IsMain && other.IsMain) return 0;
                if (IsMain && !other.IsMain) return -1;
                if (!IsMain && other.IsMain) return 1;

                if (DevLetter != other.DevLetter) return DevLetter.CompareTo(other.DevLetter);
                return MicroUpdate.CompareTo(other.MicroUpdate);
            }
        }

        #endregion
    }
}

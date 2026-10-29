using UnityEngine;
using UnityEditor;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System;

public class VersionCheckerWindow : EditorWindow
{
    private string githubOwner = "";
    private string githubRepo = "";
    private string githubToken = "";

    private string localVersion = "";
    private string statusMessage = "Нажмите 'Проверить версию'";
    private Color statusColor = Color.white;
    private bool isChecking = false;

    [MenuItem("Tools/Version Checker")]
    public static void ShowWindow()
    {
        GetWindow<VersionCheckerWindow>("Version Checker");
    }

    private void OnEnable()
    {
        githubOwner = EditorPrefs.GetString("VC_GithubOwner", "");
        githubRepo = EditorPrefs.GetString("VC_GithubRepo", "");
        githubToken = EditorPrefs.GetString("VC_GithubToken", "");
        localVersion = PlayerSettings.bundleVersion;
    }

    private void OnGUI()
    {
        GUILayout.Label("Настройки GitHub", EditorStyles.boldLabel);

        githubOwner = EditorGUILayout.TextField("Owner (Имя/Орг)", githubOwner);
        githubRepo = EditorGUILayout.TextField("Repository", githubRepo);
        githubToken = EditorGUILayout.PasswordField("Token (Опционально)", githubToken);

        if (GUILayout.Button("Сохранить настройки"))
        {
            EditorPrefs.SetString("VC_GithubOwner", githubOwner);
            EditorPrefs.SetString("VC_GithubRepo", githubRepo);
            EditorPrefs.SetString("VC_GithubToken", githubToken);
        }

        GUILayout.Space(15);
        GUILayout.Label("Проверка версии", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Локальная версия:", localVersion);

        GUILayout.Space(10);
        GUI.enabled = !isChecking && !string.IsNullOrEmpty(githubOwner) && !string.IsNullOrEmpty(githubRepo);
        if (GUILayout.Button(isChecking ? "Проверка..." : "Проверить версию"))
        {
            CheckVersionAsync();
        }
        GUI.enabled = true;

        GUILayout.Space(15);

        GUIStyle resultStyle = new GUIStyle(EditorStyles.largeLabel);
        resultStyle.normal.textColor = statusColor;
        resultStyle.wordWrap = true;
        GUILayout.Label(statusMessage, resultStyle);
    }

    private async void CheckVersionAsync()
    {
        isChecking = true;
        statusMessage = "Получение данных с GitHub...";
        statusColor = Color.yellow;
        Repaint();

        try
        {
            string commitMessage = await GetLatestMainCommitMessage();

            if (string.IsNullOrEmpty(commitMessage))
            {
                SetStatus("Не удалось получить текст коммита.", Color.red);
                return;
            }

            // Извлекаем версию из текста коммита
            string extractedVersion = ExtractVersionFromText(commitMessage);

            if (string.IsNullOrEmpty(extractedVersion))
            {
                SetStatus($"Версия не найдена в коммите. Текст: {commitMessage.Substring(0, Math.Min(50, commitMessage.Length))}...", Color.red);
                return;
            }

            if (!ParsedVersion.TryParse(localVersion, out ParsedVersion localVer))
            {
                SetStatus($"Локальная версия '{localVersion}' не соответствует формату.", Color.red);
                return;
            }

            if (!ParsedVersion.TryParse(extractedVersion, out ParsedVersion mainVer))
            {
                SetStatus($"Извлеченная версия '{extractedVersion}' не соответствует формату.", Color.red);
                return;
            }

            int comparison = localVer.CompareTo(mainVer);

            if (comparison == 0)
            {
                SetStatus($"✓ Версия актуальна (Совпадает с Main: {mainVer.OriginalString})", new Color(0.2f, 0.8f, 0.2f));
            }
            else if (comparison < 0)
            {
                SetStatus($" Версия устарела! (Локальная: {localVer.OriginalString} | Main: {mainVer.OriginalString})", new Color(0.9f, 0.2f, 0.2f));
            }
            else
            {
                SetStatus($"⚠ Экспериментальная версия (Новее Main: {localVer.OriginalString} | Main: {mainVer.OriginalString})", Color.gray);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка: {ex.Message}", Color.red);
        }
        finally
        {
            isChecking = false;
            Repaint();
        }
    }

    private void SetStatus(string message, Color color)
    {
        statusMessage = message;
        statusColor = color;
    }

    /// <summary>
    /// Извлекает первое вхождение версии из текста коммита.
    /// Приоритет: Dev формат > Main с именем > Main без имени
    /// </summary>
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
        // Важно: исключаем случаи, где после цифр идет буква (это был бы dev без точки)
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
            if (!string.IsNullOrEmpty(githubToken))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");
            }

            string url = $"https://api.github.com/repos/{githubOwner}/{githubRepo}/commits/main";
            string jsonResponse = await client.GetStringAsync(url);

            // Извлекаем значение поля "message" из JSON
            var match = Regex.Match(jsonResponse, "\"message\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (match.Success)
            {
                // Декодируем экранированные символы
                string message = match.Groups[1].Value;
                message = message.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
                return message;
            }
            return null;
        }
    }

    #region Version Parsing & Comparison Logic

    private class ParsedVersion : IComparable<ParsedVersion>
    {
        public int Major;      // Релиз
        public int Minor;      // Под_Релиз
        public int Patch;      // До_Релизная (Цифра)
        public bool IsMain;

        // Для Main ветки (опционально)
        public string DeityName;

        // Для Dev ветки
        public char DevLetter;
        public int MicroUpdate;

        public string OriginalString;

        public static bool TryParse(string input, out ParsedVersion version)
        {
            version = new ParsedVersion { OriginalString = input };

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

            // Формат Main без имени: 0.0.3 (просто релиз важной функции)
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

            // Формат Dev: 0.0.4d.3
            var matchDev = Regex.Match(input, @"^(\d+)\.(\d+)\.(\d+)([a-zA-Z])\.(\d+)$");
            if (matchDev.Success)
            {
                version.Major = int.Parse(matchDev.Groups[1].Value);
                version.Minor = int.Parse(matchDev.Groups[2].Value);
                version.Patch = int.Parse(matchDev.Groups[3].Value);
                version.DevLetter = matchDev.Groups[4].Value[0];
                version.MicroUpdate = int.Parse(matchDev.Groups[5].Value);
                version.IsMain = false;
                return true;
            }

            return false;
        }

        public int CompareTo(ParsedVersion other)
        {
            if (other == null) return 1;

            // 1. Сравниваем базовые цифры (Релиз.Под_Релиз.До_Релизная)
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch) return Patch.CompareTo(other.Patch);

            // 2. Если базовые цифры равны, смотрим на суффиксы
            // Main (релиз) считается "финальным" состоянием этой цифры.
            // Dev (с буквой и микро-обновлением) считается "новее/экспериментальным".

            if (IsMain && other.IsMain) return 0; // Оба Main -> Совпадение

            if (IsMain && !other.IsMain) return -1; // Я Main, он Dev -> Я древнее (Красный)
            if (!IsMain && other.IsMain) return 1;  // Я Dev, он Main -> Я новее (Серый)

            // 3. Оба Dev, базы равны. Сравниваем букву и микро-обновление
            if (DevLetter != other.DevLetter) return DevLetter.CompareTo(other.DevLetter);
            return MicroUpdate.CompareTo(other.MicroUpdate);
        }
    }

    #endregion
}
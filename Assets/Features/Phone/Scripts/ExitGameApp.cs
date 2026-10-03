using System.Collections;
using TMPro;
using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Выход": сначала экран с подтверждением ("Да" / "Нет"), при
    /// согласии - экран плавно гаснет (см. ScreenFader), на чёрном фоне ненадолго
    /// появляется прощальная надпись (одна из FarewellMessages - меняются прямо
    /// в инспекторе), и только после этого игра реально закрывается. В редакторе
    /// Unity вместо реального выхода останавливает Play Mode, чтобы можно было
    /// тестировать.
    ///
    /// Сам диалог - это НЕ экран внутри маленькой карточки телефона (якоря не
    /// могут выйти за пределы родителя), а отдельный объект прямо на Canvas
    /// сцены (как ScreenFader/Hightscreen), растянутый на весь экран. Иконка
    /// "Выход" в телефоне вызывает Instance.Show() напрямую (см.
    /// ExitButtonHandler) - без AppIconUI и PhoneScreenManager.
    /// </summary>
    public class ExitGameApp : MonoBehaviour
    {
        public static ExitGameApp Instance { get; private set; }

        [Tooltip("Сам объект должен ОСТАВАТЬСЯ включённым в сцене (activeSelf = true) - " +
                 "у выключенного при загрузке сцены объекта Unity вообще не вызывает Awake, " +
                 "и Instance никогда бы не установился. Показ/скрытие - через эту группу")]
        [SerializeField] private CanvasGroup _rootGroup;

        [Header("Прощание")]
        [Tooltip("Меняй прямо тут - при выходе случайно берётся одна из фраз")]
        [SerializeField] private string[] _farewellMessages = { "Спасибо, что играли!" };
        [Tooltip("Отдельная группа с прощальной надписью - на время выхода переносится " +
                 "в самый конец Canvas, чтобы оказаться поверх затемнения ScreenFader")]
        [SerializeField] private CanvasGroup _farewellGroup;
        [SerializeField] private TMP_Text _farewellText;
        [SerializeField] private float _farewellFadeDuration = 0.6f;
        [SerializeField] private float _farewellHoldTime = 1.5f;

        private void Awake()
        {
            Instance = this;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show()
        {
            SetVisible(true);
            // Кнопки могли остаться сдвинутыми/оглушёнными и с вырванными буквами
            // с прошлого открытия - GameObject-ы не выключаются, Update не сбрасывает их сам
            foreach (var btn in GetComponentsInChildren<DodgingButton>(true))
                btn.ResetState();
            foreach (var sentence in GetComponentsInChildren<ThrowableSentence>(true))
                sentence.ResetState();
            // Пока диалог открыт - Escape не должен закрывать сам телефон под ним
            PhoneUIController.Instance?.SetInputEnabled(false);
        }

        // Кнопка "Нет"
        public void Cancel()
        {
            SetVisible(false);
            PhoneUIController.Instance?.SetInputEnabled(true);
        }

        private void SetVisible(bool visible)
        {
            if (_rootGroup == null) return;
            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.interactable = visible;
            _rootGroup.blocksRaycasts = visible;
        }

        // Кнопка "Да" на экране подтверждения
        public void ConfirmExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            if (_farewellText != null && _farewellMessages.Length > 0)
                _farewellText.text = _farewellMessages[Random.Range(0, _farewellMessages.Length)];

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeToBlack();

            if (_farewellGroup != null)
            {
                _farewellGroup.gameObject.SetActive(true);
                yield return Fade(_farewellGroup, 0f, 1f, _farewellFadeDuration);
                yield return new WaitForSecondsRealtime(_farewellHoldTime);
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}

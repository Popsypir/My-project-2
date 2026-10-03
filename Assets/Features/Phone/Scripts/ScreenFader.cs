using System.Collections;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Общая чёрная панель на весь экран для плавных переходов (например звонок
    /// с последующей телепортацией).
    ///
    /// _canvasGroup должен быть настроен так: полностью закрывает экран,
    /// изначально alpha = 0 и объект неактивен (SetActive(false)) - включаем
    /// его сами, когда реально нужно затемнить.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Плавно затемняет экран до чёрного - остаётся чёрным, пока не вызовешь FadeFromBlack.
        public IEnumerator FadeToBlack()
        {
            if (_canvasGroup == null) yield break;

            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;
            yield return Fade(_canvasGroup.alpha, 1f);
        }

        // Плавно убирает черноту обратно.
        public IEnumerator FadeFromBlack()
        {
            if (_canvasGroup == null) yield break;

            yield return Fade(_canvasGroup.alpha, 0f);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.gameObject.SetActive(false);
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;

            while (t < _fadeDuration)
            {
                // unscaledDeltaTime - чтобы затемнение работало, даже если игра на паузе
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = to;
        }
    }
}

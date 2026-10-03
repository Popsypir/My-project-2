using TMPro;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Только для тестирования - по кнопке ускоряет время (Time.timeScale),
    /// чтобы не ждать по-настоящему, пока покупатели спавнятся, ходят по
    /// полкам, встают в очередь, а сложность воров растёт по стадиям.
    /// Нажатия по кругу переключают между несколькими скоростями (см. _scales).
    ///
    /// ВАЖНО: саму смену (CheckoutManager) это НЕ ускоряет - её конец
    /// специально считается по настоящему реальному времени (DateTime.Now,
    /// см. CheckoutManager.StartShift), а не через Time.timeScale, так и
    /// задумано ("смена = 5 реальных минут"). Ускоряются только спавн
    /// покупателей, их ходьба/анимации и рост сложности воров - то есть
    /// как раз то, что долго ждать при тестировании.
    /// </summary>
    public class DebugTimeScale : MonoBehaviour
    {
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [Tooltip("Скорости по кругу - первое значение всегда должно быть 1 (обычная скорость)")]
        [SerializeField] private float[] _scales = { 1f, 3f, 8f, 20f };
        [Tooltip("Необязательно - текст на экране с текущей скоростью, для наглядности при тестировании")]
        [SerializeField] private TMP_Text _indicatorText;

        private int _index;

        private void Start()
        {
            UpdateIndicator();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(_toggleKey)) return;

            _index = (_index + 1) % Mathf.Max(1, _scales.Length);
            Time.timeScale = _scales[_index];
            UpdateIndicator();
        }

        private void OnDestroy()
        {
            // На всякий случай - чтобы ускорение не "залипло", если этот
            // объект вдруг исчезнет из сцены (смена сцены и т.п.)
            Time.timeScale = 1f;
        }

        private void UpdateIndicator()
        {
            if (_indicatorText == null) return;
            _indicatorText.text = Mathf.Approximately(Time.timeScale, 1f) ? "" : $"x{Time.timeScale:0.#}";
        }
    }
}

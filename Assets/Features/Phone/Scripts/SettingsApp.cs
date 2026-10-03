using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Приложение "Настройки". Каждое значение применяется и сохраняется сразу
    /// через GameSettings (PlayerPrefs) - отдельной кнопки "Сохранить" не нужно.
    ///
    /// Помимо этих трёх настроек в самом Unity можешь добавить сколько угодно
    /// декоративных элементов (тумблеров/ползунков с забавными подписями,
    /// которые ни на что не влияют) - просто обычные Toggle/Slider в интерфейсе
    /// без подключения к этому скрипту, это нормально.
    /// </summary>
    public class SettingsApp : PhoneAppBase
    {
        [Header("Громкость")]
        [SerializeField] private Slider _volumeSlider;

        [Header("Мышь")]
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private Toggle _invertLookYToggle;

        [Header("Графика")]
        [SerializeField] private Slider _pixelationSlider;

        // Пока true - изменения ползунков/тумблеров игнорируются (это мы сами
        // выставляем им значения из GameSettings, а не игрок их крутит)
        private bool _initializing;

        private void Start()
        {
            if (_volumeSlider != null)
                _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            if (_sensitivitySlider != null)
                _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

            if (_invertLookYToggle != null)
                _invertLookYToggle.onValueChanged.AddListener(OnInvertLookYChanged);

            if (_pixelationSlider != null)
                _pixelationSlider.onValueChanged.AddListener(OnPixelationChanged);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshUIFromSettings();
        }

        // Подтягивает в UI то, что реально сохранено - на случай если игрок
        // открыл Настройки не первый раз за игру
        private void RefreshUIFromSettings()
        {
            _initializing = true;

            if (_volumeSlider != null) _volumeSlider.value = GameSettings.Volume;
            if (_sensitivitySlider != null) _sensitivitySlider.value = GameSettings.MouseSensitivity;
            if (_invertLookYToggle != null) _invertLookYToggle.isOn = GameSettings.InvertLookY;
            if (_pixelationSlider != null) _pixelationSlider.value = GameSettings.Pixelation;

            _initializing = false;
        }

        private void OnVolumeChanged(float value)
        {
            if (_initializing) return;
            GameSettings.Volume = value;
        }

        private void OnSensitivityChanged(float value)
        {
            if (_initializing) return;
            GameSettings.MouseSensitivity = value;
        }

        private void OnInvertLookYChanged(bool value)
        {
            if (_initializing) return;
            GameSettings.InvertLookY = value;
        }

        private void OnPixelationChanged(float value)
        {
            if (_initializing) return;
            GameSettings.Pixelation = value;
        }
    }
}

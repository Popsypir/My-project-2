using System;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Глобальные настройки игрока - громкость, чувствительность мыши,
    /// инверсия взгляда по Y. Сохраняются между запусками через PlayerPrefs -
    /// значение читается/пишется сразу при каждом обращении, отдельного
    /// "Save" не нужно. Камеры (FixedCameraController, FirstPersonCameraController)
    /// читают MouseSensitivity/InvertLookY каждый кадр напрямую отсюда.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "Settings_Volume";
        private const string SensitivityKey = "Settings_MouseSensitivity";
        private const string InvertLookYKey = "Settings_InvertLookY";
        private const string PixelationKey = "Settings_Pixelation";

        private static float _volume = 1f;
        private static float _mouseSensitivity = 1f;
        private static bool _invertLookY;
        private static float _pixelation;
        private static bool _loaded;

        /// <summary>Вызывается при любом изменении любой настройки</summary>
        public static event Action OnChanged;

        public static float Volume
        {
            get { EnsureLoaded(); return _volume; }
            set
            {
                EnsureLoaded();
                _volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, _volume);
                AudioListener.volume = _volume;
                OnChanged?.Invoke();
            }
        }

        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return _mouseSensitivity; }
            set
            {
                EnsureLoaded();
                _mouseSensitivity = Mathf.Clamp(value, 0.1f, 3f);
                PlayerPrefs.SetFloat(SensitivityKey, _mouseSensitivity);
                OnChanged?.Invoke();
            }
        }

        public static bool InvertLookY
        {
            get { EnsureLoaded(); return _invertLookY; }
            set
            {
                EnsureLoaded();
                _invertLookY = value;
                PlayerPrefs.SetInt(InvertLookYKey, value ? 1 : 0);
                OnChanged?.Invoke();
            }
        }

        // 0 - эффект выключен, 1 - максимальная пикселизация (см. PixelationController)
        public static float Pixelation
        {
            get { EnsureLoaded(); return _pixelation; }
            set
            {
                EnsureLoaded();
                _pixelation = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(PixelationKey, _pixelation);
                OnChanged?.Invoke();
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            _mouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            _invertLookY = PlayerPrefs.GetInt(InvertLookYKey, 0) == 1;
            _pixelation = PlayerPrefs.GetFloat(PixelationKey, 0f);

            AudioListener.volume = _volume;
        }
    }
}

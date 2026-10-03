using System.Collections;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Фоновый звук очереди (бормотание/шум покупателей). Специально настроен
    /// как 3D-звук (spatialBlend = 1) с затуханием по расстоянию - слышен
    /// только рядом с самим объектом, а не по всей сцене. Повесь на объект,
    /// стоящий в месте очереди (например, на CustomerQueueController).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class QueueAmbientAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;

        [Header("Звуки")]
        [Tooltip("Варианты звука очереди - каждый раз выбирается случайный")]
        [SerializeField] private AudioClip[] _ambientSounds;

        [Header("Интервал между звуками")]
        [SerializeField] private float _minInterval = 4f;
        [SerializeField] private float _maxInterval = 12f;

        [Header("Слышимость (3D-звук)")]
        [Tooltip("На каком расстоянии звук ещё на полной громкости")]
        [SerializeField] private float _minDistance = 2f;
        [Tooltip("На каком расстоянии звук уже совсем не слышно")]
        [SerializeField] private float _maxDistance = 10f;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            // Обязательно 3D - иначе AudioSource по умолчанию 2D, и звук был бы
            // слышен одинаково громко по всей сцене, а не только рядом с очередью.
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = _minDistance;
            _audioSource.maxDistance = _maxDistance;
        }

        private void OnEnable()
        {
            StartCoroutine(AmbientLoop());
        }

        private IEnumerator AmbientLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minInterval, _maxInterval));
                PlayRandomAmbient();
            }
        }

        private void PlayRandomAmbient()
        {
            if (_ambientSounds == null || _ambientSounds.Length == 0) return;

            AudioClip clip = _ambientSounds[Random.Range(0, _ambientSounds.Length)];
            if (clip == null) return;

            _audioSource.PlayOneShot(clip);
        }
    }
}

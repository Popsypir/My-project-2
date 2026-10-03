using System.Collections;
using UnityEngine;

namespace GamePhone.Movement
{
    public class RandomMeowPlayer : MonoBehaviour
    {
        [Tooltip("Откуда проигрывать мяуканье. Если не назначено - ищется на этом объекте")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Варианты звука мяуканья - каждый раз выбирается случайный")]
        [SerializeField] private AudioClip[] _meowSounds;

        [Header("Интервал между мяуканьями")]
        [SerializeField] private float _minInterval = 8f;
        [SerializeField] private float _maxInterval = 25f;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            StartCoroutine(MeowLoop());
        }

        private IEnumerator MeowLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minInterval, _maxInterval));
                PlayRandomMeow();
            }
        }

        private void PlayRandomMeow()
        {
            if (_audioSource == null || _meowSounds == null || _meowSounds.Length == 0) return;

            AudioClip clip = _meowSounds[Random.Range(0, _meowSounds.Length)];
            if (clip == null) return;

            _audioSource.PlayOneShot(clip);
        }
    }
}

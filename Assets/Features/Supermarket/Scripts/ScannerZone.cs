using System.Collections;
using TMPro;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Триггер-зона сканера. Товар засчитывается как отсканированный, только
    /// если провёл его через зону с "правильной" скоростью - не слишком быстро
    /// и не слишком медленно. Есть ещё и небольшой шанс, что скан просто не
    /// пробьётся, даже при правильной скорости (как в Among Us) - для азарта.
    /// При успехе/провале показывает надпись нужным цветом (зелёный/красный).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScannerZone : MonoBehaviour
    {
        [Tooltip("Необязательно - если не назначено, ищется само на сцене")]
        [SerializeField] private CheckoutManager _checkoutManager;

        [Header("Скорость сканирования")]
        [Tooltip("Медленнее этого - \"слишком медленно\"")]
        [SerializeField] private float _minGoodSpeed = 0.5f;
        [Tooltip("Быстрее этого - \"слишком быстро\"")]
        [SerializeField] private float _maxGoodSpeed = 4f;

        [Header("Случайный промах")]
        [Tooltip("Шанс, что скан не пробьётся, даже если скорость в норме (0 - никогда, 1 - всегда)")]
        [Range(0f, 1f)]
        [SerializeField] private float _missChance = 0.08f;

        [Header("Надпись обратной связи")]
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private float _feedbackDuration = 1f;
        [SerializeField] private string _successMessage = "Отсканировано!";
        [SerializeField] private string _tooSlowMessage = "Слишком медленно!";
        [SerializeField] private string _tooFastMessage = "Слишком быстро!";
        [SerializeField] private string _missMessage = "Не пробилось!";
        [SerializeField] private Color _successColor = Color.green;
        [SerializeField] private Color _failColor = Color.red;

        [Header("Звук")]
        [Tooltip("Откуда проигрывать звуки сканирования. Если не назначено - ищется на этом объекте")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Звуки при успешном сканировании товара - можно добавить несколько вариантов, " +
                 "каждый раз будет проигрываться случайный, чтобы не приедалось")]
        [SerializeField] private AudioClip[] _scanSuccessSounds;
        [Tooltip("Звуки при неудачном сканировании (слишком быстро/медленно/не пробилось) - " +
                 "тоже можно несколько вариантов")]
        [SerializeField] private AudioClip[] _scanFailSounds;

        private Coroutine _feedbackRoutine;

        private void Awake()
        {
            if (_checkoutManager == null)
                _checkoutManager = FindAnyObjectByType<CheckoutManager>();

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<ScannableItem>();
            if (item == null || item.State != ScanState.OnConveyor) return;

            float speed = item.Rb != null ? item.Rb.linearVelocity.magnitude : 0f;

            if (speed < _minGoodSpeed)
            {
                ShowFeedback(_tooSlowMessage, false);
                return;
            }

            if (speed > _maxGoodSpeed)
            {
                ShowFeedback(_tooFastMessage, false);
                return;
            }

            if (Random.value < _missChance)
            {
                ShowFeedback(_missMessage, false);
                return;
            }

            item.MarkScanned();
            _checkoutManager?.RegisterScan(item);
            ShowFeedback(_successMessage, true);
        }

        private void ShowFeedback(string message, bool success)
        {
            PlayRandomSound(success ? _scanSuccessSounds : _scanFailSounds);

            if (_feedbackText == null) return;

            _feedbackText.text = message;
            _feedbackText.color = success ? _successColor : _failColor;

            if (_feedbackRoutine != null)
                StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(ClearFeedbackAfterDelay());
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (_audioSource == null || clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            _audioSource.PlayOneShot(clip);
        }

        private IEnumerator ClearFeedbackAfterDelay()
        {
            yield return new WaitForSeconds(_feedbackDuration);
            if (_feedbackText != null)
                _feedbackText.text = string.Empty;
        }
    }
}

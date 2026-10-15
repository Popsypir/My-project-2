using System.Collections;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Ворота гаража: уезжают вверх, пропуская фургон, и опускаются обратно
    /// (см. Van - он открывает их перед приездом и закрывает после отъезда).
    ///
    /// Закрытым считается то положение, в котором ворота стоят в сцене. Открытое
    /// - на Lift Height выше. Никакой анимации рисовать не нужно, но если у
    /// модели есть свой клип, его можно положить в Gate Clip, и ворота поедут
    /// по нему, как двери фургона.
    ///
    /// Коллайдер едет вместе с воротами, поэтому проём открывается и для физики,
    /// а не только на вид.
    /// </summary>
    public class GarageGate : MonoBehaviour
    {
        [Header("Что двигаем")]
        [Tooltip("Створка ворот. Не задашь - возьму сам этот объект")]
        [SerializeField] private Transform _gate;

        [Header("Подъём")]
        [Tooltip("На сколько метров ворота уезжают вверх. 0 - возьму высоту самой створки, " +
                 "чтобы она целиком ушла из проёма")]
        [SerializeField] private float _liftHeight;
        [Tooltip("Сколько секунд занимает подъём")]
        [SerializeField] private float _openDuration = 1.4f;
        [Tooltip("Сколько секунд занимает опускание")]
        [SerializeField] private float _closeDuration = 1.2f;
        [Tooltip("Пауза перед тем, как закрыться: чтобы ворота не падали фургону на хвост")]
        [SerializeField] private float _delayBeforeClose = 0.4f;

        [Header("Звук")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Скрип ворот. Играет и на подъёме, и на опускании")]
        [SerializeField] private AudioClip _moveClip;

        /// <summary>Ворота полностью подняты.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Ворота сейчас едут.</summary>
        public bool IsMoving { get; private set; }

        private Vector3 _closedPosition;
        private bool _captured;
        private Coroutine _routine;

        private void Awake() => Capture();

        private void Capture()
        {
            if (_captured) return;
            _captured = true;

            if (_gate == null) _gate = transform;
            _closedPosition = _gate.position;

            // Высоту подъёма по умолчанию берём по самой створке: так она
            // целиком уходит вверх и не висит половиной в проёме
            if (_liftHeight <= 0f)
            {
                var r = _gate.GetComponent<Renderer>();
                _liftHeight = r != null ? r.bounds.size.y : 3f;
            }
        }

        /// <summary>Поднять ворота. Ждать окончания можно через саму корутину.</summary>
        public IEnumerator Open() => Move(true);

        /// <summary>Опустить ворота.</summary>
        public IEnumerator Close() => Move(false);

        /// <summary>Поднять, не дожидаясь, - для кнопки или триггера.</summary>
        public void OpenNow() => Restart(true);

        /// <summary>Опустить, не дожидаясь.</summary>
        public void CloseNow() => Restart(false);

        private void Restart(bool opening)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Move(opening));
        }

        private IEnumerator Move(bool opening)
        {
            Capture();

            if (opening && !IsMoving && IsOpen) yield break;    // уже наверху
            if (!opening && !IsMoving && !IsOpen) yield break;  // уже внизу

            if (!opening && _delayBeforeClose > 0f)
                yield return new WaitForSeconds(_delayBeforeClose);

            IsMoving = true;
            PlaySound();

            Vector3 from = _gate.position;
            Vector3 to = opening ? _closedPosition + Vector3.up * _liftHeight : _closedPosition;

            // Длительность считаем по оставшемуся пути: если ворота поехали вниз,
            // не доехав доверху, они не будут ползти так же долго, как с нуля
            float full = Mathf.Max(0.01f, _liftHeight);
            float part = Vector3.Distance(from, to) / full;
            float duration = Mathf.Max(0.01f, (opening ? _openDuration : _closeDuration) * part);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Трогаются и останавливаются плавно - створка тяжёлая
                _gate.position = Vector3.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }

            _gate.position = to;

            // Коллайдер створки живёт в физике и обновляется только к следующему
            // шагу - без этого проём ещё мгновение остаётся закрытым, и фургон
            // может упереться в уже поднятые ворота
            Physics.SyncTransforms();

            IsOpen = opening;
            IsMoving = false;
            StopSound();
        }

        private void PlaySound()
        {
            if (_audioSource == null || _moveClip == null) return;
            _audioSource.clip = _moveClip;
            _audioSource.Play();
        }

        private void StopSound()
        {
            if (_audioSource != null && _audioSource.clip == _moveClip) _audioSource.Stop();
        }

        private void OnDrawGizmosSelected()
        {
            Transform g = _gate != null ? _gate : transform;
            var r = g.GetComponent<Renderer>();
            float lift = _liftHeight > 0f ? _liftHeight : (r != null ? r.bounds.size.y : 3f);

            Vector3 closed = Application.isPlaying && _captured ? _closedPosition : g.position;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(closed, closed + Vector3.up * lift);
            if (r != null)
            {
                Gizmos.DrawWireCube(closed + Vector3.up * lift, r.bounds.size);
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawWireCube(closed, r.bounds.size);
            }
        }
    }
}

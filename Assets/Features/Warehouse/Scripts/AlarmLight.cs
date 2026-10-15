using System.Collections;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Аварийная мигалка на погрузку. Начинается погрузка (см. PostOfficeShift) -
    /// из потолка выезжает лампа, свет в зале приглушается, два красных луча
    /// начинают крутиться, воет сирена. Фургон уехал - всё убирается обратно.
    ///
    /// Звук можно не искать: если клип не назначен, сирена синтезируется прямо
    /// в игре - двухтональный вой, как у настоящей.
    /// </summary>
    public class AlarmLight : MonoBehaviour
    {
        [Header("Когда включается")]
        [Tooltip("Смена, за которой следим. Кончился рабочий день - тревога пошла одновременно " +
                 "с выездом фургона, фургон уехал - выключилась")]
        [SerializeField] private PostOfficeShift _shift;

        [Header("Что двигается")]
        [Tooltip("Сама лампа - она выезжает из потолка")]
        [SerializeField] private Transform _lamp;
        [Tooltip("Штанга, на которой лампа висит. Вытягивается вслед за ней")]
        [SerializeField] private Transform _rod;
        [Tooltip("Эта часть крутится - на ней висят лучи")]
        [SerializeField] private Transform _spinner;

        [Header("Выезд")]
        [Tooltip("На сколько метров лампа опускается из потолка")]
        [SerializeField] private float _dropHeight = 0.9f;
        [Tooltip("Сколько секунд занимает выезд")]
        [SerializeField] private float _dropDuration = 1.2f;

        [Header("Вращение")]
        [Tooltip("Градусов в секунду. 180 - полный оборот за две секунды")]
        [SerializeField] private float _spinSpeed = 170f;

        [Header("Свет в зале")]
        [Tooltip("Эти источники приглушаются на время тревоги")]
        [SerializeField] private Light[] _dimLights;
        [Tooltip("До какой доли от обычной яркости приглушать. 0.4 - до сорока процентов")]
        [SerializeField, Range(0.05f, 1f)] private float _dimFactor = 0.4f;
        [Tooltip("Приглушать ли и общий свет неба")]
        [SerializeField] private bool _dimAmbient = true;
        [Tooltip("Сколько секунд гаснет и разгорается свет")]
        [SerializeField] private float _dimDuration = 0.8f;

        [Header("Свет самой лампы")]
        [Tooltip("Обычный свет: горит, пока идёт смена, и гаснет на время тревоги")]
        [SerializeField] private Light _idleLight;
        [Tooltip("Тревожный свет: красная заливка и крутящиеся лучи. Горит только в тревогу")]
        [SerializeField] private Light[] _alarmLights;

        [Header("Плафон")]
        [Tooltip("Стекляшка лампы - краснеет только на время тревоги")]
        [SerializeField] private Renderer _glass;
        [Tooltip("Цвет плафона в обычное время")]
        [SerializeField] private Color _glassIdleColor = new Color(1f, 0.96f, 0.88f, 1f);
        [Tooltip("Цвет плафона в тревогу")]
        [SerializeField] private Color _glassAlarmColor = new Color(0.85f, 0.05f, 0.04f, 1f);
        [Tooltip("Насколько ярко плафон светится сам по себе")]
        [SerializeField] private float _glassGlow = 3f;

        [Header("Сирена")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Звук сирены. Пусто - синтезирую сам, двухтональный вой")]
        [SerializeField] private AudioClip _sirenClip;
        [Tooltip("Громкость сирены")]
        [SerializeField, Range(0f, 1f)] private float _volume = 0.55f;

        /// <summary>Тревога включена.</summary>
        public bool IsOn { get; private set; }

        private Vector3 _lampUpPosition;
        private float _rodBaseScaleY;
        private float[] _lightBaseIntensity;
        private float _ambientBase;
        private bool _captured;
        private Coroutine _routine;
        private AudioClip _generated;
        private MaterialPropertyBlock _glassBlock;

        private void Awake() => Capture();

        private void Capture()
        {
            if (_captured) return;
            _captured = true;

            if (_lamp == null) _lamp = transform;
            _lampUpPosition = _lamp.localPosition;
            _rodBaseScaleY = _rod != null ? _rod.localScale.y : 1f;

            _lightBaseIntensity = new float[_dimLights != null ? _dimLights.Length : 0];
            for (int i = 0; i < _lightBaseIntensity.Length; i++)
                if (_dimLights[i] != null) _lightBaseIntensity[i] = _dimLights[i].intensity;

            _ambientBase = RenderSettings.ambientIntensity;
            ApplyDrop(0f);
            SetAlarmLights(false);
        }

        private void OnEnable()
        {
            if (_shift == null) return;

            // Именно на конец дня, а не на приезд фургона: тревога должна
            // начаться ОДНОВРЕМЕННО с его выездом, а не после того, как он
            // доехал и открыл двери
            _shift.OnShiftEnded += Play;
            _shift.OnVanDeparted += Stop;
        }

        private void OnDisable()
        {
            if (_shift != null)
            {
                _shift.OnShiftEnded -= Play;
                _shift.OnVanDeparted -= Stop;
            }

            // Свет возвращаем обязательно: он живёт в настройках сцены, и
            // приглушённым остался бы даже после выхода из игры
            RestoreLights();
        }

        private void OnDestroy()
        {
            if (_generated != null) Destroy(_generated);
            SetAlarmLights(false);
        }

        /// <summary>Включить тревогу.</summary>
        public void Play() => Restart(true);

        /// <summary>Выключить тревогу.</summary>
        public void Stop() => Restart(false);

        private void Restart(bool on)
        {
            Capture();
            if (IsOn == on) return;

            IsOn = on;
            SetAlarmLights(on);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(on));
        }

        private void Update()
        {
            // Крутим только пока тревога идёт - иначе лампа вертелась бы впустую
            if (IsOn && _spinner != null)
                _spinner.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private IEnumerator Run(bool on)
        {
            if (on) StartSiren();

            float from = CurrentDrop();
            float to = on ? 1f : 0f;
            float duration = Mathf.Max(0.01f, _dropDuration * Mathf.Abs(to - from));

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                ApplyDrop(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
                yield return null;
            }

            ApplyDrop(to);
            if (!on) StopSiren();
        }

        // 0 - лампа в потолке, 1 - выехала полностью
        private float CurrentDrop()
        {
            if (_dropHeight <= 0.001f) return IsOn ? 1f : 0f;
            return Mathf.Clamp01((_lampUpPosition.y - _lamp.localPosition.y) / _dropHeight);
        }

        private void ApplyDrop(float k)
        {
            if (_lamp != null)
                _lamp.localPosition = _lampUpPosition + Vector3.down * (_dropHeight * k);

            // Штанга тянется от потолка к лампе. У цилиндра начало координат
            // в середине, поэтому мало растянуть его по высоте - надо ещё и
            // опустить на половину длины, иначе он лезет вверх сквозь потолок
            // и лампа едет вкривь
            if (_rod != null)
            {
                float length = Mathf.Max(0.02f, _dropHeight * k);
                Vector3 s = _rod.localScale;
                s.y = length * 0.5f;              // у цилиндра высота равна двум масштабам
                _rod.localScale = s;

                Vector3 p = _rod.localPosition;
                p.y = -length * 0.5f;             // верх штанги остаётся в потолке
                _rod.localPosition = p;
            }

            SetDim(k);
        }

        // Лампа всё время висит на потолке и светит как обычная. В тревогу
        // обычный свет гаснет, а вместо него загорается красный
        private void SetAlarmLights(bool alarm)
        {
            if (_idleLight != null) _idleLight.enabled = !alarm;

            if (_alarmLights != null)
                foreach (Light l in _alarmLights)
                    if (l != null) l.enabled = alarm;

            PaintGlass(alarm);
        }

        // Цвет меняем через блок свойств, а не у самого материала: материал
        // общий, и перекрасив его, мы перекрасили бы файл в проекте - плафон
        // остался бы красным навсегда, даже после выхода из игры
        private void PaintGlass(bool alarm)
        {
            if (_glass == null) return;

            _glassBlock ??= new MaterialPropertyBlock();
            _glass.GetPropertyBlock(_glassBlock);

            Color c = alarm ? _glassAlarmColor : _glassIdleColor;
            _glassBlock.SetColor("_BaseColor", c);
            _glassBlock.SetColor("_EmissionColor", c * _glassGlow);

            _glass.SetPropertyBlock(_glassBlock);
        }

        private void SetDim(float k)
        {
            float factor = Mathf.Lerp(1f, _dimFactor, k);

            for (int i = 0; i < _lightBaseIntensity.Length; i++)
                if (_dimLights[i] != null) _dimLights[i].intensity = _lightBaseIntensity[i] * factor;

            if (_dimAmbient) RenderSettings.ambientIntensity = _ambientBase * factor;
        }

        private void RestoreLights()
        {
            if (!_captured) return;

            for (int i = 0; i < _lightBaseIntensity.Length; i++)
                if (_dimLights[i] != null) _dimLights[i].intensity = _lightBaseIntensity[i];

            if (_dimAmbient) RenderSettings.ambientIntensity = _ambientBase;
        }

        private void StartSiren()
        {
            if (_audioSource == null) return;

            AudioClip clip = _sirenClip != null ? _sirenClip : (_generated ??= BuildSiren());
            _audioSource.clip = clip;
            _audioSource.loop = true;
            _audioSource.volume = _volume;
            _audioSource.Play();
        }

        private void StopSiren()
        {
            if (_audioSource != null) _audioSource.Stop();
        }

        /// <summary>
        /// Двухтональный вой: частота плавно ходит вверх-вниз, как у настоящей
        /// сирены. Нужен, только если своего звука ещё нет - назначишь клип,
        /// и это больше не вызовется.
        /// </summary>
        private AudioClip BuildSiren()
        {
            const int rate = 44100;
            const float cycle = 1.6f;      // секунда с небольшим на полный вой
            int samples = Mathf.RoundToInt(rate * cycle);

            var data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                // частота ходит между 480 и 850 герцами по синусу
                float freq = Mathf.Lerp(480f, 850f, 0.5f + 0.5f * Mathf.Sin(t / cycle * Mathf.PI * 2f));
                phase += freq / rate * Mathf.PI * 2f;

                // чуть квадратный призвук, чтобы звучало тревожно, а не как свисток
                float wave = Mathf.Sin(phase) * 0.75f + Mathf.Sign(Mathf.Sin(phase)) * 0.25f;
                data[i] = wave * 0.5f;
            }

            var clip = AudioClip.Create("Сирена", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

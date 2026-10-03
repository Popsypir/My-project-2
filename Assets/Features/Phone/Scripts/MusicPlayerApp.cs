using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// MP3-плеер внутри телефона. Играет по списку треков (клип + название +
    /// обложка), умеет вперёд/назад, плей/пауза, громкость отдельным ползунком.
    ///
    /// ВАЖНО: AudioSource (и сам этот скрипт) должны висеть на ВСЕГДА активном
    /// объекте - например на самом телефоне, а не внутри Screen Root, который
    /// PhoneAppBase включает/выключает при открытии/закрытии приложения. Иначе
    /// и музыка обрывается при закрытии плеера, и Update() тут перестаёт
    /// вызываться - трек не сможет ни доиграть сам до конца и переключиться на
    /// следующий, ни бегущая строка названия не будет ехать, пока экран плеера
    /// не открыт. AudioSource можно перетащить с любого объекта - поле ниже
    /// просто ссылка, ему всё равно, что именно на нём висит.
    /// </summary>
    public class MusicPlayerApp : PhoneAppBase
    {
        [System.Serializable]
        public class Track
        {
            public string Title;
            public AudioClip Clip;
            public Sprite Cover;
        }

        [Header("Треки")]
        [Tooltip("Плейлист - по кругу (после последнего трека снова первый)")]
        [SerializeField] private Track[] _tracks;
        [Tooltip("Начинать проигрывание первого трека сразу при первом открытии плеера")]
        [SerializeField] private bool _autoPlayOnFirstOpen = false;

        [Header("Звук")]
        [Tooltip("Живёт на постоянно активном объекте (например на самом телефоне) - см. заметку класса выше. " +
                 "Если не назначено - на всякий случай ищется на этом же объекте")]
        [SerializeField] private AudioSource _audioSource;
        [Range(0f, 1f)]
        [SerializeField] private float _defaultVolume = 0.5f;

        [Header("UI - окно трека")]
        [SerializeField] private Image _coverImage;
        [SerializeField] private TMP_Text _trackTitleText;
        [Tooltip("Необязательно - например «2 / 7»")]
        [SerializeField] private TMP_Text _trackIndexText;

        [Header("UI - бегущая строка названия (если не помещается)")]
        [Tooltip("Объект-\"окошко\" с обрезкой (Mask / Rect Mask 2D), внутри которого лежит _trackTitleText - " +
                 "по ширине ЭТОГО объекта определяется, влезает название целиком или нет")]
        [SerializeField] private RectTransform _trackTitleViewport;
        [Tooltip("Скорость прокрутки, юнитов интерфейса в секунду")]
        [SerializeField] private float _marqueeSpeed = 40f;
        [Tooltip("Пауза у каждого края перед тем, как поехать обратно")]
        [SerializeField] private float _marqueeEdgePause = 1f;

        [Header("UI - громкость (шуточный пружинный слайдер - см. SpringSlider)")]
        [SerializeField] private SpringSlider _volumeSpringSlider;
        [Tooltip("Необязательно - число рядом, например \"70%\"")]
        [SerializeField] private TMP_Text _volumeText;

        [Header("UI - перемотка трека")]
        [Tooltip("Min Value = 0, Max Value = 1, Whole Numbers выключен")]
        [SerializeField] private Slider _progressSlider;
        [Tooltip("Необязательно - повесь на тот же объект, что и Progress Slider (см. SliderPointerTracker), " +
                 "чтобы ползунок не дёргался назад, пока его тащишь мышкой")]
        [SerializeField] private SliderPointerTracker _progressSliderPointerTracker;
        [Tooltip("Слева от ползунка - сколько уже прошло, формат мм:сс")]
        [SerializeField] private TMP_Text _elapsedTimeText;
        [Tooltip("Справа от ползунка - сколько осталось до конца трека, формат мм:сс")]
        [SerializeField] private TMP_Text _remainingTimeText;

        private int _currentIndex;
        private bool _hasStarted;
        private Coroutine _marqueeRoutine;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            if (_volumeSpringSlider != null)
                _volumeSpringSlider.OnValueChanged += SetVolume;

            if (_progressSlider != null)
                _progressSlider.onValueChanged.AddListener(OnProgressSliderChanged);
        }

        public override void OnOpen()
        {
            base.OnOpen();

            // Плейлист заводим только один раз - если игрок уже включал музыку,
            // выходил на другой экран и вернулся, трек/позиция/пауза не сбрасываются.
            if (!_hasStarted)
            {
                _hasStarted = true;

                if (_audioSource != null)
                {
                    _audioSource.volume = _defaultVolume;
                    _audioSource.loop = false;
                }

                if (_tracks != null && _tracks.Length > 0)
                    LoadTrack(0, playImmediately: _autoPlayOnFirstOpen);
            }

            if (_volumeSpringSlider != null && _audioSource != null)
                _volumeSpringSlider.SetValueWithoutNotify(_audioSource.volume);
            UpdateVolumeVisuals();

            // Название текущего трека могло быть загружено раньше, чем открыли
            // экран (плеер играет в фоне) - бегущую строку нужно завести заново.
            RestartMarquee();
        }

        private void Update()
        {
            if (!_hasStarted || _audioSource == null || _audioSource.clip == null) return;

            // Трек доиграл сам до конца (не был поставлен на паузу игроком) -
            // отличаем от паузы по тому, что дошли почти до самого конца клипа.
            // ВАЖНО: тут нельзя вызывать обычный NextTrack() - он сам смотрит на
            // _audioSource.isPlaying, чтобы понять "играть дальше или нет", а
            // клип к этому моменту уже сам остановился (isPlaying == false),
            // хотя на самом деле трек только что играл - поэтому здесь явно
            // говорим "играть дальше" (playImmediately: true), не полагаясь на isPlaying.
            bool reachedEnd = !_audioSource.isPlaying && _audioSource.time >= _audioSource.clip.length - 0.05f;
            if (reachedEnd)
                LoadTrack(_currentIndex + 1, playImmediately: true);

            if (_progressSlider != null && _audioSource.clip != null)
            {
                bool isDragging = _progressSliderPointerTracker != null && _progressSliderPointerTracker.IsPressed;
                if (!isDragging)
                    _progressSlider.SetValueWithoutNotify(_audioSource.time / _audioSource.clip.length);
            }

            if (_elapsedTimeText != null)
                _elapsedTimeText.text = FormatTime(_audioSource.time);

            if (_remainingTimeText != null)
                _remainingTimeText.text = FormatTime(_audioSource.clip.length - _audioSource.time);
        }

        // мм:сс, минуты без ведущего нуля (как в большинстве плееров), секунды - с ним
        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{minutes}:{secs:00}";
        }

        // Вешается на кнопку "вперёд"
        public void NextTrack()
        {
            if (_tracks == null || _tracks.Length == 0) return;
            bool wasPlaying = _audioSource != null && _audioSource.isPlaying;
            LoadTrack(_currentIndex + 1, wasPlaying);
        }

        // Вешается на кнопку "назад"
        public void PreviousTrack()
        {
            if (_tracks == null || _tracks.Length == 0) return;
            bool wasPlaying = _audioSource != null && _audioSource.isPlaying;
            LoadTrack(_currentIndex - 1, wasPlaying);
        }

        // Вешается на кнопку плей/пауза (иконка у неё одна и не меняется -
        // просто переключает воспроизведение)
        public void TogglePlayPause()
        {
            if (_audioSource == null || _audioSource.clip == null) return;

            if (_audioSource.isPlaying)
                _audioSource.Pause();
            else
                _audioSource.UnPause();
        }

        // Вызывается сама через SpringSlider.OnValueChanged (см. Start) - и пока
        // тащишь ручку мышкой, и пока она сама качается на "пружине" после отпускания
        public void SetVolume(float value)
        {
            if (_audioSource != null)
                _audioSource.volume = value;

            UpdateVolumeVisuals();
        }

        private void UpdateVolumeVisuals()
        {
            if (_volumeText == null || _audioSource == null) return;
            _volumeText.text = $"{Mathf.RoundToInt(_audioSource.volume * 100f)}%";
        }

        // Вызывается через onValueChanged слайдера перемотки (см. Start) - value от 0 до 1.
        // Срабатывает и от перетаскивания мышкой, и от клика по дорожке слайдера - на программные
        // обновления из Update (SetValueWithoutNotify) это событие не реагирует, зацикливания нет.
        private void OnProgressSliderChanged(float value)
        {
            if (_audioSource == null || _audioSource.clip == null) return;
            _audioSource.time = Mathf.Clamp01(value) * _audioSource.clip.length;
        }

        private void LoadTrack(int index, bool playImmediately)
        {
            if (_tracks == null || _tracks.Length == 0 || _audioSource == null) return;

            // Зацикленный плейлист - после последнего трека снова первый, и наоборот
            _currentIndex = ((index % _tracks.Length) + _tracks.Length) % _tracks.Length;
            Track track = _tracks[_currentIndex];

            _audioSource.clip = track.Clip;

            // Play() + сразу Pause(), если не должны играть - так UnPause() потом
            // сможет нормально продолжить именно с начала этого трека.
            _audioSource.Play();
            if (!playImmediately)
                _audioSource.Pause();

            if (_coverImage != null) _coverImage.sprite = track.Cover;
            if (_trackTitleText != null) _trackTitleText.text = track.Title;
            if (_trackIndexText != null) _trackIndexText.text = $"{_currentIndex + 1} / {_tracks.Length}";

            RestartMarquee();
        }

        // Перезапускает бегущую строку названия трека - если оно не влезает в
        // видимую область (_trackTitleViewport), едет туда-обратно с паузами по
        // краям; если помещается целиком - просто стоит на месте слева, без анимации.
        private void RestartMarquee()
        {
            if (_marqueeRoutine != null)
            {
                StopCoroutine(_marqueeRoutine);
                _marqueeRoutine = null;
            }

            if (_trackTitleText == null) return;

            RectTransform textRect = _trackTitleText.rectTransform;
            Vector2 pos = textRect.anchoredPosition;
            pos.x = 0f;
            textRect.anchoredPosition = pos;

            if (_trackTitleViewport == null) return;

            // Текст пересчитывается Unity только в конце кадра - форсируем сейчас,
            // чтобы preferredWidth уже отражал только что назначенный текст.
            _trackTitleText.ForceMeshUpdate();

            float overflow = _trackTitleText.preferredWidth - _trackTitleViewport.rect.width;
            if (overflow > 0f)
                _marqueeRoutine = StartCoroutine(MarqueeRoutine(textRect, overflow));
        }

        private IEnumerator MarqueeRoutine(RectTransform textRect, float scrollDistance)
        {
            float duration = scrollDistance / Mathf.Max(1f, _marqueeSpeed);

            while (true)
            {
                yield return new WaitForSeconds(_marqueeEdgePause);
                yield return MoveMarquee(textRect, 0f, -scrollDistance, duration);

                yield return new WaitForSeconds(_marqueeEdgePause);
                yield return MoveMarquee(textRect, -scrollDistance, 0f, duration);
            }
        }

        private IEnumerator MoveMarquee(RectTransform textRect, float fromX, float toX, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float x = Mathf.Lerp(fromX, toX, Mathf.Clamp01(t / duration));
                textRect.anchoredPosition = new Vector2(x, textRect.anchoredPosition.y);
                yield return null;
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Video;

namespace GamePhone.World
{
    /// <summary>
    /// Телевизор: показывает каналы на своём экране. Канал - это либо видео,
    /// либо неподвижная картинка, либо помехи (рябь, которая рисуется сама).
    ///
    /// Всё рисуется в одну Render Texture, а материал экрана на неё уже смотрит -
    /// поэтому при переключении канала сам материал трогать не нужно.
    ///
    /// Переключается ударом лапы (см. TvPunch), но NextChannel можно дёрнуть и
    /// откуда угодно - например, повесить на кнопку.
    /// </summary>
    public class TvSet : MonoBehaviour
    {
        public enum ChannelOrder
        {
            Sequential,   // по кругу: первый, второй, третий, снова первый
            Random        // вразнобой, но никогда дважды подряд один и тот же
        }

        public enum ChannelKind
        {
            Video,   // проигрывается видеоклип
            Still,   // просто картинка
            Noise    // помехи: рябь, которая генерируется на лету
        }

        [Serializable]
        public class Channel
        {
            [Tooltip("Для подписи в инспекторе, в игре не показывается")]
            public string Name = "Канал";
            public ChannelKind Kind = ChannelKind.Video;
            [Tooltip("Только для канала-видео")]
            public VideoClip Clip;
            [Tooltip("Только для канала-картинки")]
            public Texture Still;
        }

        [Header("Экран")]
        [Tooltip("Куда рисуется картинка. Эту же текстуру должен показывать материал экрана")]
        [SerializeField] private RenderTexture _screenTexture;
        [Tooltip("Проигрыватель видео на этом же объекте. Не назначишь - найду сам")]
        [SerializeField] private VideoPlayer _videoPlayer;

        [Header("Каналы")]
        [Tooltip("Список каналов. Чтобы добавить новый: нажми плюсик, выбери тип (видео, картинка или помехи) и кинь нужный файл")]
        [SerializeField] private Channel[] _channels;
        [Tooltip("С какого канала телевизор включается при начале смены")]
        [SerializeField] private int _startChannel;
        [Tooltip("Как переключаются каналы с каждым ударом: по порядку или вразнобой")]
        [SerializeField] private ChannelOrder _order = ChannelOrder.Random;

        [Header("Помехи")]
        [Tooltip("Размер зерна ряби. Чем меньше, тем крупнее шум и тем дешевле он обходится")]
        [SerializeField] private int _noiseSize = 64;
        [Tooltip("Сколько раз в секунду обновляется рябь")]
        [SerializeField] private float _noiseFps = 12f;

        [Header("Звук")]
        [Tooltip("Громкость при переключении канала не трогается - это просто общий уровень")]
        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;

        public int ChannelCount => _channels != null ? _channels.Length : 0;
        public int CurrentChannel { get; private set; }

        /// <summary>Канал переключился - пригодится звуку или подписи на экране.</summary>
        public event Action<int> OnChannelChanged;

        private Texture2D _noiseTexture;
        private float _nextNoiseTime;
        private bool _showingNoise;

        private void Awake()
        {
            if (_videoPlayer == null) _videoPlayer = GetComponent<VideoPlayer>();

            if (_videoPlayer != null)
            {
                // Рисуем строго в Render Texture: при MaterialOverride плеер лезет
                // прямо в материал экрана, и неподвижные каналы с помехами он бы
                // затирал своей картинкой
                _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoPlayer.targetTexture = _screenTexture;
                _videoPlayer.isLooping = true;
                _videoPlayer.playOnAwake = false;
                _videoPlayer.SetDirectAudioVolume(0, _volume);
            }
        }

        private void Start()
        {
            CurrentChannel = ChannelCount == 0 ? 0 : Mathf.Clamp(_startChannel, 0, ChannelCount - 1);
            ApplyChannel();
        }

        private void OnDestroy()
        {
            if (_noiseTexture != null) Destroy(_noiseTexture);
        }

        private void Update()
        {
            if (!_showingNoise || _screenTexture == null) return;
            if (Time.time < _nextNoiseTime) return;

            _nextNoiseTime = Time.time + (_noiseFps > 0f ? 1f / _noiseFps : 0.1f);
            DrawNoise();
        }

        /// <summary>Переключить канал - по порядку или на случайный, смотря как настроено.</summary>
        public void NextChannel()
        {
            if (ChannelCount == 0) return;

            CurrentChannel = _order == ChannelOrder.Random
                ? PickRandomChannel()
                : (CurrentChannel + 1) % ChannelCount;

            ApplyChannel();
            OnChannelChanged?.Invoke(CurrentChannel);
        }

        // Любой канал, кроме того, что идёт сейчас: иначе удар иногда выглядел
        // бы так, будто телевизор его не заметил
        private int PickRandomChannel()
        {
            if (ChannelCount <= 1) return 0;

            int next = UnityEngine.Random.Range(0, ChannelCount - 1);
            return next >= CurrentChannel ? next + 1 : next;
        }

        public void SetChannel(int index)
        {
            if (ChannelCount == 0) return;

            // Кольцом, а не обрезкой: так -1 честно даёт последний канал
            CurrentChannel = ((index % ChannelCount) + ChannelCount) % ChannelCount;
            ApplyChannel();
            OnChannelChanged?.Invoke(CurrentChannel);
        }

        public Channel Current => ChannelCount == 0 ? null : _channels[CurrentChannel];

        private void ApplyChannel()
        {
            _showingNoise = false;

            if (ChannelCount == 0) return;
            Channel ch = _channels[CurrentChannel];

            switch (ch.Kind)
            {
                case ChannelKind.Video:
                    if (_videoPlayer == null || ch.Clip == null) { Blackout(); return; }
                    _videoPlayer.clip = ch.Clip;
                    _videoPlayer.Play();
                    break;

                case ChannelKind.Still:
                    StopVideo();
                    if (ch.Still != null && _screenTexture != null) Graphics.Blit(ch.Still, _screenTexture);
                    else Blackout();
                    break;

                case ChannelKind.Noise:
                    StopVideo();
                    _showingNoise = true;
                    _nextNoiseTime = 0f;
                    DrawNoise();
                    break;
            }
        }

        private void StopVideo()
        {
            if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
        }

        private void Blackout()
        {
            if (_screenTexture == null) return;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _screenTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        // Рябь рисуем в маленькую текстуру и растягиваем на весь экран - по пикселю
        // на полный размер экрана это стоило бы в разы дороже, а выглядит так же
        private void DrawNoise()
        {
            int size = Mathf.Max(8, _noiseSize);

            if (_noiseTexture == null || _noiseTexture.width != size)
            {
                if (_noiseTexture != null) Destroy(_noiseTexture);
                _noiseTexture = new Texture2D(size, size, TextureFormat.RGB24, false)
                {
                    filterMode = FilterMode.Point
                };
            }

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)UnityEngine.Random.Range(0, 256);
                pixels[i] = new Color32(v, v, v, 255);
            }

            _noiseTexture.SetPixels32(pixels);
            _noiseTexture.Apply(false);

            if (_screenTexture != null) Graphics.Blit(_noiseTexture, _screenTexture);
        }
    }
}

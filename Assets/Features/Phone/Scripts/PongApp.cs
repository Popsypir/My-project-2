using TMPro;
using UnityEngine;

namespace GamePhone.Apps
{
    /// <summary>
    /// Простой пинг-понг внутри телефона - чтобы охраннику было не скучно,
    /// пока он присматривает за камерами. Управление мышью: ракетка игрока
    /// следует за курсором по вертикали, курсор всё равно свободен, пока
    /// телефон открыт (см. PhoneCursorController). С какой стороны поля
    /// расставить Player Paddle/Ai Paddle - не важно, отскок считается по
    /// фактическому положению, а не по жёстко заданной стороне.
    ///
    /// Границы ракеток/мяча берутся через РЕАЛЬНЫЕ мировые углы объекта
    /// (RectTransform.GetWorldCorners), переведённые в локальные координаты
    /// _playArea - а не через anchoredPosition ± половина ширины. Это
    /// специально не зависит от того, как настроены anchors/pivot у
    /// конкретного объекта - двигать pivot руками не нужно, всё считается
    /// по факту.
    /// </summary>
    public class PongApp : PhoneAppBase
    {
        [Header("Поле")]
        [Tooltip("Панель, внутри которой всё происходит - от её размера считаются границы поля")]
        [SerializeField] private RectTransform _playArea;
        [Tooltip("Нужна только для Canvas в режиме Screen Space - Camera/World Space. " +
                 "Для Screen Space - Overlay оставь пустым")]
        [SerializeField] private Camera _uiCamera;

        [Header("Ракетки")]
        [SerializeField] private RectTransform _playerPaddle;
        [SerializeField] private RectTransform _aiPaddle;
        [SerializeField] private float _aiSpeed = 300f;

        [Header("Мяч")]
        [SerializeField] private RectTransform _ball;
        [SerializeField] private float _ballStartSpeed = 250f;
        [SerializeField] private float _ballSpeedGain = 15f;
        [Tooltip("Пауза перед подачей после каждого гола")]
        [SerializeField] private float _serveDelay = 0.8f;

        [Header("Счёт")]
        [SerializeField] private TMP_Text _scoreText;

        [Header("Пауза")]
        [Tooltip("Панель с кнопками 'Продолжить'/'Начать заново'/'Выход' - обычная часть экрана Pong " +
                 "внутри телефона, прячется вместе с ним (и вообще вместе с этим экраном), когда телефон сворачивается")]
        [SerializeField] private GameObject _pausePanel;
        [Tooltip("Кто переключает экраны телефона - нужно, чтобы кнопка 'Выход' вернула на домашний экран")]
        [SerializeField] private PhoneScreenManager _screenManager;

        private bool _isOpen;
        private bool _isPaused;
        private bool _hasStarted; // сбрасываем счёт/мяч только один раз, при самом первом входе
        private Vector2 _ballVelocity;
        private float _ballSpeed;
        private float _serveTimer;
        private int _playerScore;
        private int _aiScore;

        private readonly Vector3[] _cornersBuffer = new Vector3[4];

        public override void OnOpen()
        {
            base.OnOpen();
            _isOpen = true;

            // Счёт/мяч сбрасываем только при самом первом входе - если игрок уже
            // играл, вышел на главный экран и вернулся обратно, всё (счёт, пауза,
            // положение мяча) должно остаться как было, а не начинаться заново.
            if (!_hasStarted)
            {
                _hasStarted = true;
                _isPaused = false;
                if (_pausePanel != null) _pausePanel.SetActive(false);
                _playerScore = 0;
                _aiScore = 0;
                UpdateScoreText();
                ServeBall(Random.value < 0.5f ? 1f : -1f);
            }
        }

        public override void OnClose()
        {
            _isOpen = false;
            // Паузу/панель/счёт намеренно НЕ сбрасываем - см. OnOpen выше. Сама
            // панель - обычная часть экрана Pong, гаснет вместе с ним автоматически,
            // никакого отдельного управления видимостью ей не нужно.
            base.OnClose();
        }

        // Вешается на кнопку паузы в инспекторе (OnClick), и вызывается сами же
        // по Escape во время игры (см. Update)
        public void PauseGame()
        {
            if (!_isOpen || _isPaused) return;
            _isPaused = true;
            if (_pausePanel != null) _pausePanel.SetActive(true);
        }

        // Вешается на кнопку "Продолжить" в панели паузы
        public void ResumeGame()
        {
            if (!_isPaused) return;
            _isPaused = false;
            if (_pausePanel != null) _pausePanel.SetActive(false);
        }

        // Вешается на кнопку "Начать заново" в панели паузы - сбрасывает счёт и
        // подаёт мяч заново, как при самом первом входе
        public void RestartGame()
        {
            _isPaused = false;
            if (_pausePanel != null) _pausePanel.SetActive(false);
            _playerScore = 0;
            _aiScore = 0;
            UpdateScoreText();
            ServeBall(Random.value < 0.5f ? 1f : -1f);
        }

        // Вешается на кнопку "Выход" в панели паузы - возвращает на домашний
        // экран телефона (не закрывает игру целиком, для этого есть ExitGameApp).
        // Сама пауза при этом остаётся как есть - вернувшись в пинг-понг, увидим
        // то же самое меню паузы (см. OnOpen).
        public void ExitToHome()
        {
            if (_screenManager != null)
                _screenManager.ShowHome();
            else
                OnClose();
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Первый же Escape во время игры - и меню паузы включается, и
            // телефон сворачивается ОДНОВРЕМЕННО (это отдельная панель поверх
            // экрана - см. _pausePanel, она не спрячется вместе с телефоном).
            // Пока на паузе - Escape уже просто открывает/закрывает телефон
            // как обычно (PhoneUIController сам это обрабатывает), само меню
            // убирают только кнопки "Продолжить"/"Начать заново"/"Выход".
            if (!_isPaused && Input.GetKeyDown(KeyCode.Escape))
            {
                PauseGame();
                PhoneUIController.Instance?.Close();
            }

            if (_playArea == null || _isPaused) return;

            UpdatePlayerPaddle();
            UpdateAiPaddle();
            UpdateBall();
        }

        // Центр rt (по реальным мировым углам, не по pivot), в локальных
        // координатах _playArea - (0,0) это центр поля.
        private Vector2 GetLocalPosition(RectTransform rt)
        {
            GetLocalBounds(rt, out float minX, out float maxX, out float minY, out float maxY);
            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        // Реальные границы rt (левый/правый/нижний/верхний край), переведённые
        // в локальные координаты _playArea - через мировые углы объекта, а не
        // через anchoredPosition ± половина размера (это зависело бы от pivot).
        private void GetLocalBounds(RectTransform rt, out float minX, out float maxX, out float minY, out float maxY)
        {
            rt.GetWorldCorners(_cornersBuffer);

            // Порядок из GetWorldCorners: [0] нижний левый, [2] верхний правый -
            // этого достаточно, чтобы получить обе границы по X и по Y.
            Vector2 a = _playArea.InverseTransformPoint(_cornersBuffer[0]);
            Vector2 b = _playArea.InverseTransformPoint(_cornersBuffer[2]);

            minX = Mathf.Min(a.x, b.x);
            maxX = Mathf.Max(a.x, b.x);
            minY = Mathf.Min(a.y, b.y);
            maxY = Mathf.Max(a.y, b.y);
        }

        // Двигает rt так, чтобы его ЦЕНТР (не pivot) оказался в targetLocalCenter -
        // считает разницу между текущим центром и нужным, и сдвигает объект
        // ровно на эту разницу. Это не зависит от того, где именно у объекта
        // pivot - просто "переносит" его целиком на нужное расстояние.
        private void SetLocalPosition(RectTransform rt, Vector2 targetLocalCenter)
        {
            Vector2 currentLocalCenter = GetLocalPosition(rt);
            Vector2 localDelta = targetLocalCenter - currentLocalCenter;
            rt.position += _playArea.TransformVector(localDelta);
        }

        private void UpdatePlayerPaddle()
        {
            if (_playerPaddle == null) return;

            Vector2 localPoint;
            bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _playArea, Input.mousePosition, _uiCamera, out localPoint);

            if (!inside) return;

            float halfHeight = _playArea.rect.height * 0.5f;
            GetLocalBounds(_playerPaddle, out _, out _, out float minY, out float maxY);
            float paddleHalfHeight = (maxY - minY) * 0.5f;
            float clampedY = Mathf.Clamp(localPoint.y, -halfHeight + paddleHalfHeight, halfHeight - paddleHalfHeight);

            Vector2 pos = GetLocalPosition(_playerPaddle);
            pos.y = clampedY;
            SetLocalPosition(_playerPaddle, pos);
        }

        private void UpdateAiPaddle()
        {
            if (_aiPaddle == null || _ball == null) return;

            float halfHeight = _playArea.rect.height * 0.5f;
            GetLocalBounds(_aiPaddle, out _, out _, out float minY, out float maxY);
            float paddleHalfHeight = (maxY - minY) * 0.5f;

            Vector2 pos = GetLocalPosition(_aiPaddle);
            Vector2 ballPos = GetLocalPosition(_ball);
            float targetY = Mathf.Clamp(ballPos.y, -halfHeight + paddleHalfHeight, halfHeight - paddleHalfHeight);
            pos.y = Mathf.MoveTowards(pos.y, targetY, _aiSpeed * Time.deltaTime);
            SetLocalPosition(_aiPaddle, pos);
        }

        private void UpdateBall()
        {
            if (_ball == null) return;

            if (_serveTimer > 0f)
            {
                _serveTimer -= Time.deltaTime;
                return;
            }

            // Реальный размер мяча - он квадратный, не круглый, поэтому
            // полу-ширина и полу-высота считаются отдельно, по его же
            // фактическому RectTransform (как у ракеток), а не по одной
            // ручной цифре "радиуса".
            GetLocalBounds(_ball, out float ballMinX, out float ballMaxX, out float ballMinY, out float ballMaxY);
            float ballHalfWidth = (ballMaxX - ballMinX) * 0.5f;
            float ballHalfHeight = (ballMaxY - ballMinY) * 0.5f;

            Vector2 pos = GetLocalPosition(_ball) + _ballVelocity * Time.deltaTime;

            float halfWidth = _playArea.rect.width * 0.5f;
            float halfHeight = _playArea.rect.height * 0.5f;

            // Отскок от верха/низа поля
            if (pos.y - ballHalfHeight < -halfHeight || pos.y + ballHalfHeight > halfHeight)
            {
                _ballVelocity.y = -_ballVelocity.y;
                pos.y = Mathf.Clamp(pos.y, -halfHeight + ballHalfHeight, halfHeight - ballHalfHeight);
            }

            TryBouncePaddle(_playerPaddle, ballHalfWidth, ballHalfHeight, ref pos);
            TryBouncePaddle(_aiPaddle, ballHalfWidth, ballHalfHeight, ref pos);

            // Гол - мяч улетел за левый или правый край поля
            if (pos.x - ballHalfWidth < -halfWidth)
            {
                _aiScore++;
                UpdateScoreText();
                ServeBall(1f);
                return;
            }

            if (pos.x + ballHalfWidth > halfWidth)
            {
                _playerScore++;
                UpdateScoreText();
                ServeBall(-1f);
                return;
            }

            SetLocalPosition(_ball, pos);
        }

        // Не зависит от того, с какой стороны поля реально стоит эта ракетка -
        // сторона столкновения (слева/справа от ракетки) считается на месте,
        // по фактическому положению мяча относительно неё, а границы ракетки -
        // по её реальным мировым углам (см. GetLocalBounds).
        private void TryBouncePaddle(RectTransform paddle, float ballHalfWidth, float ballHalfHeight, ref Vector2 ballPos)
        {
            if (paddle == null) return;

            GetLocalBounds(paddle, out float minX, out float maxX, out float minY, out float maxY);
            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            float halfHeight = (maxY - minY) * 0.5f;

            bool ballIsLeftOfPaddle = ballPos.x <= centerX;
            bool movingTowardsPaddle = ballIsLeftOfPaddle ? _ballVelocity.x > 0f : _ballVelocity.x < 0f;

            if (!movingTowardsPaddle) return;

            bool withinX = ballIsLeftOfPaddle
                ? ballPos.x + ballHalfWidth >= minX
                : ballPos.x - ballHalfWidth <= maxX;

            bool withinY = Mathf.Abs(ballPos.y - centerY) <= halfHeight + ballHalfHeight;

            if (!withinX || !withinY) return;

            _ballSpeed += _ballSpeedGain;

            // Угол отскока зависит от того, куда именно попал мяч по высоте ракетки -
            // как в классическом понге, а не просто зеркальное отражение.
            float hitOffset = Mathf.Clamp((ballPos.y - centerY) / halfHeight, -1f, 1f);
            float bounceDirX = ballIsLeftOfPaddle ? -1f : 1f;
            Vector2 direction = new Vector2(bounceDirX, hitOffset).normalized;
            _ballVelocity = direction * _ballSpeed;

            ballPos.x = ballIsLeftOfPaddle ? minX - ballHalfWidth : maxX + ballHalfWidth;
        }

        private void ServeBall(float directionX)
        {
            _serveTimer = _serveDelay;
            _ballSpeed = _ballStartSpeed;

            if (_ball != null)
                SetLocalPosition(_ball, Vector2.zero);

            float angle = Random.Range(-0.35f, 0.35f);
            _ballVelocity = new Vector2(directionX, angle).normalized * _ballSpeed;
        }

        private void UpdateScoreText()
        {
            if (_scoreText != null)
                _scoreText.text = $"{_playerScore} : {_aiScore}";
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone.Apps
{
    /// <summary>
    /// Две модели поведения (см. _chase):
    /// - Обычная (флаг выключен, например "Да") - убегает от курсора, пока тот
    ///   не подойдёт ближе dodgeRadius. Уйти дальше границ area не может -
    ///   поэтому её можно загнать в угол и там поймать.
    /// - Погоня (_chase = true, например "Нет") - наоборот, сама бежит К
    ///   курсору и, приблизившись, "упрашивает" нажать себя лёгкой пульсацией.
    ///   Такая кнопка не может быть оглушена буквами (см. ThrowableSentence) -
    ///   это не цель для бросков.
    ///
    /// Двигаться перестаёт (IsStunned), если по ней попало достаточно букв -
    /// тогда она падает вниз экрана и остаётся там неподвижной.
    ///
    /// GameObject кнопки остаётся активным даже когда сам диалог скрыт (иначе
    /// не сработал бы Awake) - поэтому Update молча выходит, пока диалог не
    /// показан, а ResetState() возвращает кнопку на стартовую позицию при
    /// каждом новом открытии диалога (см. ExitGameApp.Show()).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DodgingButton : MonoBehaviour
    {
        [Tooltip("Если включено - кнопка бежит К курсору и упрашивает нажать себя, " +
                 "вместо того чтобы убегать. Такую кнопку нельзя оглушить буквами")]
        [SerializeField] private bool _chase;

        [Tooltip("Границы, за которые кнопка не может выйти - обычно родительский " +
                 "прямоугольник диалога. Если не задано - берётся родитель кнопки")]
        [SerializeField] private RectTransform _area;

        [Tooltip("Как близко должен подойти курсор, чтобы кнопка начала убегать (только в обычном режиме)")]
        [SerializeField] private float _dodgeRadius = 160f;

        [SerializeField] private float _dodgeSpeed = 1400f;

        [Header("Погоня (только _chase)")]
        [SerializeField] private float _chaseSpeed = 900f;
        [Tooltip("На каком расстоянии от курсора кнопка останавливается - не залезает прямо под указатель")]
        [SerializeField] private float _chaseStopDistance = 55f;
        [SerializeField] private float _beggingPulseSpeed = 6f;
        [SerializeField] private float _beggingPulseAmount = 0.08f;

        [SerializeField] private float _maxTiltAngle = 20f;
        [SerializeField] private float _tiltSmoothing = 10f;

        [Header("Оглушение буквами (только обычный режим)")]
        [Tooltip("Сколько букв должно попасть, чтобы кнопка упала и перестала убегать")]
        [SerializeField] private int _hitsToStun = 4;
        [SerializeField] private float _fallDuration = 0.45f;
        [SerializeField] private float _bottomMargin = 24f;
        [SerializeField] private Color _hpFullColor = new Color(0.25f, 0.85f, 0.3f);
        [SerializeField] private Color _hpLowColor = new Color(0.9f, 0.15f, 0.15f);

        public bool IsStunned { get; private set; }
        public bool IsChaser => _chase;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private Canvas _canvas;
        private CanvasGroup _dialogGroup;
        private TMP_Text _text;
        private Vector2 _startAnchoredPos;
        private int _hitCount;
        private Coroutine _fallRoutine;
        private RectTransform _hpBar;
        private Image _hpFill;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _parentRect = _rect.parent as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _dialogGroup = GetComponentInParent<CanvasGroup>();
            _text = GetComponent<TMP_Text>();
            if (_area == null) _area = _parentRect;
            _startAnchoredPos = _rect.anchoredPosition;

            // ХП-бар нужен только тому, кого реально можно "бить" буквами
            if (!_chase) CreateHpBar();
        }

        private void CreateHpBar()
        {
            var barGo = new GameObject("HpBar", typeof(RectTransform));
            barGo.transform.SetParent(_rect, false);
            _hpBar = barGo.GetComponent<RectTransform>();
            _hpBar.sizeDelta = new Vector2(180f, 16f);
            _hpBar.anchoredPosition = new Vector2(0f, _rect.rect.height * 0.5f + 26f);

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(_hpBar, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            bgImg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(_hpBar, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            _hpFill = fillGo.AddComponent<Image>();
            _hpFill.color = _hpFullColor;
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _hpFill.fillAmount = 1f;
            _hpFill.raycastTarget = false;
        }

        private void UpdateHpBar()
        {
            if (_hpFill == null) return;
            float remaining = Mathf.Clamp01(1f - (float)_hitCount / _hitsToStun);
            _hpFill.fillAmount = remaining;
            _hpFill.color = Color.Lerp(_hpLowColor, _hpFullColor, remaining);
        }

        // Вызывается из ExitGameApp.Show() при каждом открытии диалога -
        // возвращает кнопку на исходное место и снимает оглушение.
        public void ResetState()
        {
            if (_fallRoutine != null) { StopCoroutine(_fallRoutine); _fallRoutine = null; }
            IsStunned = false;
            _hitCount = 0;
            _rect.anchoredPosition = _startAnchoredPos;
            _rect.localRotation = Quaternion.identity;
            _rect.localScale = Vector3.one;
            if (_hpBar != null) _hpBar.gameObject.SetActive(true);
            UpdateHpBar();
        }

        public void RegisterHit()
        {
            if (_chase || IsStunned) return;
            _hitCount++;
            UpdateHpBar();
            if (_hitCount >= _hitsToStun)
                _fallRoutine = StartCoroutine(FallStunRoutine());
            else
                StartCoroutine(HitFlashRoutine());
        }

        // Яркая обратная связь при попадании буквой - тычок масштабом, красная
        // вспышка текста и небольшой шейк позиции, даже если это ещё не
        // последний удар до оглушения
        private IEnumerator HitFlashRoutine()
        {
            const float duration = 0.2f;
            Color baseColor = _text != null ? _text.color : Color.white;
            Vector2 basePos = _rect.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                float scale = 1f + 0.35f * Mathf.Sin(p * Mathf.PI);
                _rect.localScale = new Vector3(scale, scale, 1f);
                if (_text != null) _text.color = Color.Lerp(Color.red, baseColor, p);

                float shake = (1f - p) * 10f;
                _rect.anchoredPosition = basePos + Random.insideUnitCircle * shake;

                yield return null;
            }
            _rect.localScale = Vector3.one;
            _rect.anchoredPosition = basePos;
            if (_text != null) _text.color = baseColor;
        }

        private IEnumerator FallStunRoutine()
        {
            IsStunned = true;
            if (_hpBar != null) _hpBar.gameObject.SetActive(false);
            Vector2 start = _rect.anchoredPosition;
            float halfArea = _area != null ? _area.rect.height * 0.5f : 0f;
            float halfSelf = _rect.rect.height * 0.5f;
            float bottomY = -halfArea + halfSelf + _bottomMargin;
            Vector2 end = new Vector2(start.x, bottomY);

            float startTilt = _rect.localEulerAngles.z;
            if (startTilt > 180f) startTilt -= 360f;

            float t = 0f;
            while (t < _fallDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / _fallDuration;
                _rect.anchoredPosition = Vector2.Lerp(start, end, p);
                _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startTilt, 0f, p));
                yield return null;
            }

            _rect.anchoredPosition = end;
            _rect.localRotation = Quaternion.identity;
            _fallRoutine = null;
        }

        private void Update()
        {
            if (IsStunned) return;
            if (_dialogGroup != null && _dialogGroup.alpha < 0.5f) return;
            if (_parentRect == null || _canvas == null) return;

            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, Input.mousePosition, cam, out Vector2 localMouse))
                return;

            if (_chase) UpdateChase(localMouse);
            else UpdateFlee(localMouse);
        }

        private void UpdateChase(Vector2 localMouse)
        {
            Vector2 toCursor = localMouse - _rect.anchoredPosition;
            float dist = toCursor.magnitude;

            if (dist > _chaseStopDistance)
            {
                Vector2 dir = toCursor / dist;
                Vector2 next = _rect.anchoredPosition + dir * _chaseSpeed * Time.unscaledDeltaTime;
                _rect.anchoredPosition = ClampToArea(next);
            }

            // "упрашивающая" пульсация - живая, зовущая нажать
            float pulse = 1f + _beggingPulseAmount * Mathf.Sin(Time.unscaledTime * _beggingPulseSpeed);
            _rect.localScale = new Vector3(pulse, pulse, 1f);
        }

        private void UpdateFlee(Vector2 localMouse)
        {
            Vector2 away = _rect.anchoredPosition - localMouse;
            float dist = away.magnitude;
            float targetTilt = 0f;

            if (dist < _dodgeRadius && dist > 0.001f)
            {
                Vector2 dir = away / dist;
                float strength = 1f - (dist / _dodgeRadius);
                Vector2 next = _rect.anchoredPosition + dir * strength * _dodgeSpeed * Time.unscaledDeltaTime;
                _rect.anchoredPosition = ClampToArea(next);
                targetTilt = Mathf.Clamp(-dir.x * _maxTiltAngle, -_maxTiltAngle, _maxTiltAngle);
            }

            float currentTilt = _rect.localEulerAngles.z;
            if (currentTilt > 180f) currentTilt -= 360f;
            float newTilt = Mathf.Lerp(currentTilt, targetTilt, Time.unscaledDeltaTime * _tiltSmoothing);
            _rect.localRotation = Quaternion.Euler(0f, 0f, newTilt);
        }

        private Vector2 ClampToArea(Vector2 pos)
        {
            if (_area == null) return pos;

            Vector2 halfArea = _area.rect.size * 0.5f;
            Vector2 halfSelf = _rect.rect.size * 0.5f;

            float minX = -halfArea.x + halfSelf.x;
            float maxX = halfArea.x - halfSelf.x;
            float minY = -halfArea.y + halfSelf.y;
            float maxY = halfArea.y - halfSelf.y;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            return pos;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone.Apps
{
    /// <summary>
    /// Вешается на текст вопроса ("Вы точно хотите выйти из игры?"). Игрок
    /// хватает букву мышью (зажал на символе) и тащит - буква летит за курсором;
    /// при отпускании она летит дальше по инерции последнего рывка и, если
    /// долетает до DodgingButton, наносит удар (см. DodgingButton.RegisterHit)
    /// с короткой анимацией попадания. Промазал - буква падает и остаётся лежать
    /// на полу навсегда (не гаснет, не исчезает) - и её можно схватить и
    /// бросить ещё раз (см. ThrowableLetter, который висит на самой букве).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class ThrowableSentence : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        [Tooltip("Если не задано - ищутся все DodgingButton в этом же диалоге")]
        [SerializeField] private DodgingButton[] _targets;
        [SerializeField] private float _hitRadius = 70f;
        [SerializeField] private float _throwPower = 2.5f;
        [SerializeField] private float _minThrowSpeed = 150f;
        [Tooltip("Летящая буква крупнее, чем в самом тексте, чтобы её было видно и удобно ловить")]
        [SerializeField] private float _letterFontScale = 1.3f;

        [Header("Физика полёта (буквы не улетают за экран, не исчезают)")]
        [SerializeField] private float _gravity = 2200f;
        [SerializeField] private float _bounceDamping = 0.35f;
        [SerializeField] private float _floorFriction = 0.6f;
        [SerializeField] private float _edgeMargin = 24f;
        [SerializeField] private float _maxFlightTime = 5f;

        private TMP_Text _text;
        private Canvas _canvas;
        private RectTransform _dialogRoot;
        private readonly HashSet<int> _thrownIndices = new HashSet<int>();
        private ThrowableLetter _activeLetter;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _canvas = GetComponentInParent<Canvas>();
            _dialogRoot = GetComponentInParent<CanvasGroup>().GetComponent<RectTransform>();
            if (_targets == null || _targets.Length == 0)
                _targets = _dialogRoot.GetComponentsInChildren<DodgingButton>(true);
        }

        // Сбрасывает вырванные буквы обратно в текст - вызывается из ExitGameApp.Show()
        public void ResetState()
        {
            _thrownIndices.Clear();
            _text.ForceMeshUpdate();
        }

        private Camera EventCamera => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        // Клик по фразе - вырывает букву. Драг ведёт САМА ThrowableSentence
        // (а не созданная буква) - перекидывать eventData.pointerDrag на новый
        // объект ненадёжно: StandaloneInputModule сразу после OnPointerDown
        // сам переписывает pointerDrag, ища IDragHandler на исходном объекте
        // клика (текст фразы), а не на только что созданной букве - из-за
        // этого один клик "зависал", и хватать приходилось второй раз.
        public void OnPointerDown(PointerEventData eventData)
        {
            int index = TMP_TextUtilities.FindIntersectingCharacter(_text, eventData.position, EventCamera, true);
            if (index < 0 || _thrownIndices.Contains(index)) return;

            var charInfo = _text.textInfo.characterInfo[index];
            if (!charInfo.isVisible || char.IsWhiteSpace(charInfo.character)) return;

            _thrownIndices.Add(index);

            Vector3 worldStart = GetCharacterWorldCenter(charInfo);
            char letter = charInfo.character;
            HideCharacter(index);

            _activeLetter = CreateFlyingLetter(letter, worldStart);
            _activeLetter.BeginGrab(eventData.position);
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) => _activeLetter?.UpdateDrag(eventData.position);

        public void OnEndDrag(PointerEventData eventData) => ReleaseActiveLetter();
        public void OnPointerUp(PointerEventData eventData) => ReleaseActiveLetter();

        private void ReleaseActiveLetter()
        {
            _activeLetter?.EndGrab();
            _activeLetter = null;
        }

        public DodgingButton FindHitTarget(Vector3 worldPos)
        {
            foreach (var t in _targets)
            {
                // Убегающие от курсора кнопки убегают - "просящие" (Нет) не оглушить, не цель для бросков
                if (t == null || t.IsStunned || t.IsChaser) continue;
                if (Vector3.Distance(t.transform.position, worldPos) <= _hitRadius)
                    return t;
            }
            return null;
        }

        public Vector3 ScreenToWorld(Vector2 screenPos)
        {
            var canvasRect = _canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, EventCamera, out Vector3 world);
            return world;
        }

        public Vector2 ComputeThrowVelocity(Vector3 rawVelocity)
        {
            Vector2 velocity = (Vector2)rawVelocity * _throwPower;
            if (velocity.magnitude < _minThrowSpeed)
                velocity = velocity.normalized * _minThrowSpeed;
            return velocity;
        }

        private Vector3 GetCharacterWorldCenter(TMP_CharacterInfo info)
        {
            Vector3 localCenter = (info.topLeft + info.bottomRight) * 0.5f;
            return _text.transform.TransformPoint(localCenter);
        }

        private void HideCharacter(int index)
        {
            var textInfo = _text.textInfo;
            var charInfo = textInfo.characterInfo[index];
            var colors = textInfo.meshInfo[charInfo.materialReferenceIndex].colors32;
            int vIdx = charInfo.vertexIndex;
            for (int i = 0; i < 4; i++)
            {
                var c = colors[vIdx + i];
                c.a = 0;
                colors[vIdx + i] = c;
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private ThrowableLetter CreateFlyingLetter(char letter, Vector3 worldStart)
        {
            var go = new GameObject("FlyingLetter", typeof(RectTransform));
            // Родитель - сам диалог (не Canvas), чтобы буква физически не могла
            // выйти за его границы (см. FlyRoutine) - но SetAsLastSibling всё
            // равно рисует её поверх фона/текста/кнопок
            go.transform.SetParent(_dialogRoot, false);
            go.transform.SetAsLastSibling();

            var rt = go.GetComponent<RectTransform>();
            rt.position = worldStart;
            rt.sizeDelta = new Vector2(60f, 60f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = letter.ToString();
            tmp.font = _text.font;
            tmp.fontSharedMaterial = _text.fontSharedMaterial;
            tmp.fontStyle = _text.fontStyle;
            tmp.fontWeight = _text.fontWeight;
            tmp.fontSize = _text.fontSize * _letterFontScale;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            // raycastTarget=true - иначе букву нельзя будет схватить повторно после падения
            tmp.raycastTarget = true;

            var letterComp = go.AddComponent<ThrowableLetter>();
            letterComp.Init(this);
            return letterComp;
        }

        // Простая физика с гравитацией: буква не улетает за края диалога -
        // отскакивает от стен, падает и остаётся лежать на полу. Буквы никогда
        // не гаснут и не уничтожаются - попала она в кнопку или промазала,
        // она навсегда остаётся видимым предметом в диалоге и её можно взять снова.
        public IEnumerator FlyRoutine(RectTransform letterRect, Vector2 velocity)
        {
            float halfW = _dialogRoot.rect.width * 0.5f - _edgeMargin;
            float halfH = _dialogRoot.rect.height * 0.5f - _edgeMargin;
            float floorY = -halfH;
            float ceilingY = halfH;

            bool landed = false;
            bool hasHit = false;
            float totalTime = 0f;

            while (!landed)
            {
                if (letterRect == null) yield break;

                float dt = Time.unscaledDeltaTime;
                totalTime += dt;

                velocity.y -= _gravity * dt;
                Vector2 pos = letterRect.anchoredPosition + velocity * dt;

                if (pos.x < -halfW) { pos.x = -halfW; velocity.x = -velocity.x * _bounceDamping; }
                else if (pos.x > halfW) { pos.x = halfW; velocity.x = -velocity.x * _bounceDamping; }

                if (pos.y > ceilingY) { pos.y = ceilingY; velocity.y = -velocity.y * _bounceDamping; }

                if (pos.y <= floorY)
                {
                    pos.y = floorY;
                    velocity.y = -velocity.y * _bounceDamping;
                    velocity.x *= _floorFriction;
                    if (Mathf.Abs(velocity.y) < 60f) { velocity.y = 0f; landed = true; }
                }

                letterRect.anchoredPosition = pos;
                letterRect.localRotation *= Quaternion.Euler(0f, 0f, velocity.magnitude * 0.1f * dt);

                if (!hasHit)
                {
                    var hit = FindHitTarget(letterRect.position);
                    if (hit != null)
                    {
                        hasHit = true;
                        hit.RegisterHit();
                        StartCoroutine(PunchScale(letterRect));
                    }
                }

                if (!landed && totalTime > _maxFlightTime)
                    break; // подстраховка на случай бесконечных мелких отскоков

                yield return null;
            }

            // буква просто остаётся лежать здесь навсегда - не гаснет, не исчезает,
            // её можно взять и бросить ещё раз (ThrowableLetter.OnPointerDown)
        }

        // Короткий визуальный "тычок" при попадании - буква не пропадает, продолжает падать дальше
        private IEnumerator PunchScale(RectTransform letterRect)
        {
            const float duration = 0.18f;
            float t = 0f;
            while (t < duration)
            {
                if (letterRect == null) yield break;
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                float scale = Mathf.Lerp(1.6f, 1f, p);
                letterRect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (letterRect != null) letterRect.localScale = Vector3.one;
        }
    }
}

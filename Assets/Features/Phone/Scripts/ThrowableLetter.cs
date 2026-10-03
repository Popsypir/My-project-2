using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone.Apps
{
    /// <summary>
    /// Отдельная летающая/упавшая буква. Можно схватить и бросить снова в любой
    /// момент - и пока она ещё летит, и пока лежит на полу после предыдущего
    /// броска (грабить заново останавливает текущий полёт). Вся физика и логика
    /// попадания - в ThrowableSentence.
    ///
    /// Пока держишь - буква висит прямо на курсоре (позиция = позиция курсора
    /// каждый кадр), слегка покачиваясь как маятник.
    ///
    /// Реализует IPointerDownHandler/IDragHandler и т.п. САМА НА СЕБЕ - это
    /// нужно только для повторного захвата уже упавшей буквы (тогда курсор
    /// кликает прямо по ней, и Unity естественным образом находит эти
    /// обработчики). Для первого захвата буквы прямо из фразы этим НЕ
    /// пользуются - см. ThrowableSentence, которая сама реализует драг и
    /// вызывает BeginGrab/UpdateDrag/EndGrab напрямую (перекидывать
    /// eventData.pointerDrag на новый объект ненадёжно - StandaloneInputModule
    /// перезаписывает его сразу после OnPointerDown, ища IDragHandler на
    /// исходном объекте клика, а не на только что созданном).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ThrowableLetter : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private ThrowableSentence _owner;
        private RectTransform _rect;
        private Coroutine _flightCoroutine;
        private Vector3 _prevPointerWorldPos;
        private Vector3 _lastVelocity;

        public void Init(ThrowableSentence owner)
        {
            _owner = owner;
            _rect = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData) => BeginGrab(eventData.position);
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) => UpdateDrag(eventData.position);
        public void OnEndDrag(PointerEventData eventData) => EndGrab();
        public void OnPointerUp(PointerEventData eventData) => EndGrab();

        public void BeginGrab(Vector2 screenPos)
        {
            if (_flightCoroutine != null)
            {
                StopCoroutine(_flightCoroutine);
                _flightCoroutine = null;
            }
            _prevPointerWorldPos = _owner.ScreenToWorld(screenPos);
            _lastVelocity = Vector3.zero;
        }

        // Буква висит прямо на курсоре - позиция обновляется каждый кадр вслед за ним
        public void UpdateDrag(Vector2 screenPos)
        {
            Vector3 world = _owner.ScreenToWorld(screenPos);
            _lastVelocity = (world - _prevPointerWorldPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            _prevPointerWorldPos = world;
            _rect.position = world;

            // лёгкое покачивание маятником, как будто висит на курсоре
            float tilt = Mathf.Clamp(-_lastVelocity.x * 0.02f, -25f, 25f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        public void EndGrab()
        {
            _rect.localRotation = Quaternion.identity;

            Vector2 velocity = _owner.ComputeThrowVelocity(_lastVelocity);
            _flightCoroutine = StartCoroutine(_owner.FlyRoutine(_rect, velocity));
        }
    }
}

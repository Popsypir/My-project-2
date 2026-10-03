using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone
{
    /// <summary>
    /// Вешается на тот же объект, где Slider - показывает, тащит ли его сейчас
    /// игрок мышкой (или только что кликнул). Нужно другим скриптам, которые
    /// каждый кадр сами обновляют значение этого слайдера (например ползунок
    /// перемотки трека под реальное время воспроизведения) - пока игрок его
    /// тащит, такое авто-обновление нужно пропускать, иначе ползунок будет
    /// дёргаться назад вместо того чтобы послушно следовать за мышкой.
    /// </summary>
    public class SliderPointerTracker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsPressed { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => IsPressed = true;
        public void OnPointerUp(PointerEventData eventData) => IsPressed = false;
    }
}

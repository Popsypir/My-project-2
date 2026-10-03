using System.Collections;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Общая дверь для входа и выхода покупателей. Считает, сколько сейчас
    /// активных "просьб открыть" (RequestOpen/ReleaseOpen) - закрывается,
    /// только когда все, кто её открывал, отпустили. Так вход (CustomerSpawner)
    /// и выход (CustomerQueueController) могут пользоваться ОДНОЙ дверью, даже
    /// если совпадут по времени, не дёргая её друг у друга.
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        [SerializeField] private Transform _door;
        [SerializeField] private float _openLocalYAngle = 90f;
        [SerializeField] private float _speed = 180f;

        private int _openRequests;

        public IEnumerator RequestOpen()
        {
            _openRequests++;
            yield return StartCoroutine(RotateTo(_openLocalYAngle));
        }

        public IEnumerator ReleaseOpen()
        {
            _openRequests = Mathf.Max(0, _openRequests - 1);

            if (_openRequests == 0)
                yield return StartCoroutine(RotateTo(0f));
        }

        private IEnumerator RotateTo(float targetLocalYAngle)
        {
            if (_door == null) yield break;

            Quaternion target = Quaternion.Euler(0f, targetLocalYAngle, 0f);

            while (Quaternion.Angle(_door.localRotation, target) > 0.5f)
            {
                _door.localRotation = Quaternion.RotateTowards(_door.localRotation, target, _speed * Time.deltaTime);
                yield return null;
            }

            _door.localRotation = target;
        }
    }
}

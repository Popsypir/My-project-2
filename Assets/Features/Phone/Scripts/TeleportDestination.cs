using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Точка на сцене, куда можно телепортировать игрока. Разместите такой объект
    /// в нужном месте локации и задайте уникальный Destination Id — этот же Id
    /// нужно прописать в поле DestinationId у PhoneNumberEntry, чтобы номер вёл сюда.
    /// </summary>
    public class TeleportDestination : MonoBehaviour
    {
        [SerializeField] private string _destinationId;
        public string DestinationId => _destinationId;

        // Просто чтобы точку было видно в Scene view при выборе объекта
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}

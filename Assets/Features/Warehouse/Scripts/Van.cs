using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Фургон с ограниченной вместимостью. Коробки укладываются по слотам
    /// (простая сетка внутри кузова) - как только слоты кончились, TryAccept
    /// возвращает false, и коробка остаётся у игрока/падает на пол снаружи.
    /// </summary>
    public class Van : MonoBehaviour
    {
        [SerializeField] private int _capacity = 12;
        [Tooltip("Куда складываются принятые коробки - если не задано, используется сам Van")]
        [SerializeField] private Transform _cargoRoot;
        [Tooltip("Размер сетки слотов внутри кузова (столбцы x ряды x этажи)")]
        [SerializeField] private Vector3Int _grid = new Vector3Int(3, 4, 1);
        [SerializeField] private Vector3 _slotSize = new Vector3(0.6f, 0.6f, 0.6f);
        [SerializeField] private Vector3 _slotOrigin = new Vector3(-0.6f, 0.3f, -0.9f);

        public int Capacity => _capacity;
        public int Count { get; private set; }
        public bool IsFull => Count >= _capacity;

        private void Awake()
        {
            if (_cargoRoot == null) _cargoRoot = transform;
        }

        public bool TryAccept(Box box)
        {
            if (box == null || IsFull) return false;

            Vector3 localSlot = SlotLocalPosition(Count);
            var slotGo = new GameObject($"Slot_{Count}");
            slotGo.transform.SetParent(_cargoRoot, false);
            slotGo.transform.localPosition = localSlot;
            slotGo.transform.localRotation = Quaternion.identity;

            box.PlaceInVan(slotGo.transform);
            Count++;
            return true;
        }

        // Уезжает в конце смены - забирает с собой ровно то, что успело влезть (Count),
        // остальное (если игрок не успел) остаётся стоять на складе до следующего дня.
        public int Depart()
        {
            int taken = Count;
            Count = 0;
            gameObject.SetActive(false);
            return taken;
        }

        private Vector3 SlotLocalPosition(int index)
        {
            int perFloor = Mathf.Max(1, _grid.x * _grid.y);
            int floor = index / perFloor;
            int rem = index % perFloor;
            int col = rem % Mathf.Max(1, _grid.x);
            int row = rem / Mathf.Max(1, _grid.x);

            return _slotOrigin + new Vector3(col * _slotSize.x, floor * _slotSize.y, row * _slotSize.z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            int total = Mathf.Max(_capacity, _grid.x * _grid.y * Mathf.Max(1, _grid.z));
            for (int i = 0; i < total; i++)
            {
                Vector3 world = transform.TransformPoint(SlotLocalPosition(i));
                Gizmos.DrawWireCube(world, _slotSize * 0.9f);
            }
        }
    }
}

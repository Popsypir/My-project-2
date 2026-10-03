using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Выкладывает товары текущего покупателя на конвейер по одному, с паузой
    /// между ними. Точки спавна МОЖНО переиспользовать (например если их всего
    /// 2, а товаров в заказе больше) - перед тем как занять точку повторно,
    /// скрипт ждёт, пока предыдущий товар с неё уберут (иначе он заспавнится
    /// внутри старого и физика раскидает оба в стороны).
    /// </summary>
    public class ConveyorSpawner : MonoBehaviour
    {
        [Tooltip("Точки на конвейере, куда выкладываются товары")]
        [SerializeField] private Transform[] _spawnPoints;

        [Tooltip("Радиус проверки \"точка свободна\" вокруг каждой точки спавна")]
        [SerializeField] private float _spawnCheckRadius = 0.2f;

        public IEnumerator SpawnItemsStaggered(List<ShopItemData> order, float delayBetween, Action<ScannableItem> onItemSpawned)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogWarning("[ConveyorSpawner] Не назначены точки спавна (_spawnPoints)", this);
                yield break;
            }

            for (int i = 0; i < order.Count; i++)
            {
                ShopItemData data = order[i];
                if (data == null || data.ItemPrefab == null) continue;

                Transform point = _spawnPoints[i % _spawnPoints.Length];

                // Ждём, пока точка освободится, если на ней ещё лежит предыдущий товар
                while (IsPointOccupied(point.position))
                    yield return null;

                GameObject instance = Instantiate(data.ItemPrefab, point.position, point.rotation);

                ScannableItem item = instance.GetComponent<ScannableItem>();
                if (item == null)
                {
                    Debug.LogWarning($"[ConveyorSpawner] На префабе '{data.ItemPrefab.name}' нет компонента ScannableItem", instance);
                    Destroy(instance);
                    continue;
                }

                item.Initialize(data);
                onItemSpawned?.Invoke(item);

                yield return new WaitForSeconds(delayBetween);
            }
        }

        private bool IsPointOccupied(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, _spawnCheckRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<ScannableItem>() != null)
                    return true;
            }
            return false;
        }
    }
}

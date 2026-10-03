using UnityEngine;

namespace GamePhone.Shop
{
    public enum ScanState
    {
        OnConveyor,
        Scanned,
        Bagged
    }

    /// <summary>
    /// Вешается (уже в префабе товара) на 3D-модель, которую можно тащить мышью,
    /// сканировать и упаковывать. Требует Rigidbody - им же двигает ItemDragController.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ScannableItem : MonoBehaviour
    {
        public ShopItemData Data { get; private set; }
        public ScanState State { get; private set; } = ScanState.OnConveyor;
        public Rigidbody Rb { get; private set; }

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
        }

        public void Initialize(ShopItemData data)
        {
            Data = data;
        }

        public void MarkScanned()
        {
            if (State == ScanState.OnConveyor)
                State = ScanState.Scanned;
        }

        public void MarkBagged()
        {
            if (State == ScanState.Scanned)
                State = ScanState.Bagged;
        }
    }
}

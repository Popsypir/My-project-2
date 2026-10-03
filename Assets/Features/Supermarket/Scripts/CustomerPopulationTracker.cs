using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Вешается CustomerSpawner на каждого им же заспавненного покупателя -
    /// узнать, когда именно этот покупатель пропадает из магазина (обслужили
    /// и он провалился в яму, поймали вора и вышвырнули, вор сам сбежал и
    /// т.п.), и служит живым реестром всех, кто сейчас в магазине - см.
    /// статический Active, используется ShiftEndSequence, чтобы в конце
    /// смены разогнать вообще всех, где бы каждый сейчас ни находился.
    /// </summary>
    public class CustomerPopulationTracker : MonoBehaviour
    {
        // Все покупатели, которые сейчас реально в магазине (заспавнены и
        // ещё не удалены) - см. ShiftEndSequence.SendAllCustomersHome
        public static readonly List<CustomerPopulationTracker> Active = new();

        private CustomerSpawner _spawner;
        private CustomerShopper _shopper;
        private Coroutine _shoppingCoroutine;

        // true, если покупатель уже передан в очередь/стал вором - им
        // занимается CustomerQueueController, а не CustomerShopper, поэтому
        // ForceLeave его больше не трогает (см. CustomerShopper.ShoppingRoutine)
        private bool _hasLeftShoppingStage;

        private void Awake()
        {
            Active.Add(this);
        }

        public void Initialize(CustomerSpawner spawner)
        {
            _spawner = spawner;
        }

        // Вызывается CustomerSpawner сразу после CustomerShopper.StartShopping -
        // нужно, чтобы ForceLeave мог оборвать именно поход ЭТОГО покупателя по
        // полкам (StartShopping общий на всех сразу, каждый идёт своей корутиной)
        public void SetShoppingState(CustomerShopper shopper, Coroutine shoppingCoroutine)
        {
            _shopper = shopper;
            _shoppingCoroutine = shoppingCoroutine;
        }

        public void MarkLeftShoppingStage()
        {
            _hasLeftShoppingStage = true;
        }

        // Вызывается ShiftEndSequence в конце смены - если покупатель ещё
        // ходит по полкам, обрывает текущий поход и отправляет прямо к
        // выходу. Тех, кто уже в очереди или ворует, не трогает - их
        // экстренным выходом занимается CustomerQueueController.ForceAllToExit()
        // и собственная логика воровства (CustomerThief) соответственно.
        public void ForceLeave(Transform exitPoint)
        {
            if (_hasLeftShoppingStage || _shopper == null || exitPoint == null) return;

            if (_shoppingCoroutine != null)
                _shopper.StopCoroutine(_shoppingCoroutine);

            _shopper.SendToExit(transform, exitPoint);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            _spawner?.NotifyCustomerLeft();
        }
    }
}

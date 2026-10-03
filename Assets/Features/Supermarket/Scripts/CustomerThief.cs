using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Держит состояние "этот покупатель сейчас вор" и список украденных товаров.
    /// Добавляется на покупателя динамически (см. CustomerShopper), когда тот
    /// решает уйти без оплаты. Поимка (см. ItemDragController.TryGrabThief -
    /// ЛКМ по вору, пока он идёт к выходу, как при хватании товара) - трясёт,
    /// роняет под ним настоящие физические
    /// товары (см. DropStolenItems - охранник дальше сам подбирает их и относит
    /// на полки, см. ShopShelfStock.OnTriggerEnter), и вышвыривает вора тем же
    /// маршрутом до двери и в яму, что и обслуженного покупателя (см.
    /// CustomerQueueController.EjectThief).
    /// </summary>
    public class CustomerThief : MonoBehaviour
    {
        [Tooltip("Сколько секунд трясти пойманного вора, прежде чем вышвырнуть")]
        [SerializeField] private float _shakeDuration = 1.5f;
        [Tooltip("Насколько высоко над собой ронять товары (чтобы реально упали, а не появились внутри пола)")]
        [SerializeField] private float _dropHeight = 1f;
        [Tooltip("Насколько раскидать товары по кругу вокруг вора, чтобы не появлялись друг в друге")]
        [SerializeField] private float _dropSpread = 0.3f;

        public bool IsThief { get; private set; }

        private List<ShopItemData> _stolenItems;
        private CustomerQueueController _queue;
        private Animator _animator;
        private Rigidbody _rb;
        private PushStagger _stagger;

        // true с момента MarkAsThief и до самого удаления объекта - в отличие от
        // IsThief (которое становится false уже в момент поимки), нужен, чтобы
        // OnDestroy знал, снимать ли этого покупателя с учёта в TheftDifficultyManager
        private bool _wasEverThief;

        // Вызывается CustomerShopper сразу после того, как решил, что этот
        // покупатель - вор (нужны те же ссылки, что и для похода в очередь).
        public void MarkAsThief(List<ShopItemData> stolenItems, CustomerQueueController queue,
                                 Animator animator, Rigidbody rb, PushStagger stagger)
        {
            IsThief = true;
            _wasEverThief = true;
            _stolenItems = stolenItems;
            _queue = queue;
            _animator = animator;
            _rb = rb;
            _stagger = stagger;
        }

        // Вор пропал из магазина - неважно, поймали и вышвырнули или сам сбежал -
        // освобождаем его место в лимите TheftDifficultyManager для следующего.
        private void OnDestroy()
        {
            if (_wasEverThief)
                TheftDifficultyManager.Instance?.RegisterThiefEnded();
        }

        // Вызывается ItemDragController, когда игрок поймал вора (ЛКМ на нём под прицелом)
        public void GetCaught()
        {
            if (!IsThief) return;
            IsThief = false; // сразу - чтобы второй раз не поймать, пока идёт поимка

            SecurityStats.RegisterThiefCaught();

            StartCoroutine(CaughtRoutine());
        }

        // Вызывается CustomerQueueController, когда вор дошёл до двери НЕ пойманным -
        // считаем его сбежавшим, а всё унесённое - потерянным безвозвратно.
        public void NotifyEscaped()
        {
            SecurityStats.RegisterThiefEscaped();

            if (_stolenItems == null) return;

            for (int i = 0; i < _stolenItems.Count; i++)
                SecurityStats.RegisterItemLost();
        }

        private IEnumerator CaughtRoutine()
        {
            // Полностью застывает на время тряски - кинематическое тело физика
            // вообще не трогает (ни остаточная скорость, ни толчки, ничего),
            // поэтому он гарантированно стоит на месте, а не "доезжает" куда-то
            // по инерции. Если на Animator есть свой параметр/стейт тряски -
            // его можно включить прямо здесь же.
            if (_rb != null)
                _rb.isKinematic = true;

            yield return new WaitForSeconds(_shakeDuration);

            // Возвращаем физику - дальше им снова управляет обычная логика
            // выхода (CustomerQueueController.ExitRoutine).
            if (_rb != null)
                _rb.isKinematic = false;

            DropStolenItems();

            _queue?.EjectThief(transform, _animator, _rb, _stagger);
        }

        // Спавнит настоящие физические копии украденных товаров прямо под вором -
        // те же префабы, что и на конвейере (Rigidbody+Collider+ScannableItem
        // включены), падают и раскатываются по-настоящему. Дальше их подбирает
        // и относит на полки уже сам игрок (охранник) - см. ShopShelfStock.
        private void DropStolenItems()
        {
            if (_stolenItems == null || _stolenItems.Count == 0) return;

            Vector3 dropOrigin = transform.position + Vector3.up * _dropHeight;

            foreach (ShopItemData item in _stolenItems)
            {
                if (item == null || item.ItemPrefab == null) continue;

                Vector2 offset = Random.insideUnitCircle * _dropSpread;
                Vector3 spawnPos = dropOrigin + new Vector3(offset.x, 0f, offset.y);

                GameObject dropped = Instantiate(item.ItemPrefab, spawnPos, Random.rotation);

                ScannableItem scannable = dropped.GetComponentInChildren<ScannableItem>();
                scannable?.Initialize(item);
            }
        }
    }
}

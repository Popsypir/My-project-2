using System.Collections;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// NPC-продавец на кассе - раньше товары через сканер/упаковку вручную
    /// мышкой тащил игрок (см. ScannerZone/ItemDragController), теперь это
    /// делает сам "продавец" без участия игрока: каждые _processInterval
    /// секунд берёт следующий ещё не отсканированный товар с конвейера
    /// (CheckoutManager.GetNextUnscannedItem), засчитывает скан, затем через
    /// небольшую паузу - упаковку. Игрок теперь только охраняет магазин.
    ///
    /// Ничего не хватает и не двигает физически - "пробивание" полностью
    /// логическое (как договорились для первой версии). Если позже понадобится
    /// анимация рук - её можно добавить сюда же, между RegisterScan и
    /// RegisterBagged.
    /// </summary>
    public class AutoCashier : MonoBehaviour
    {
        [Tooltip("Необязательно - если не назначено, ищется само на сцене")]
        [SerializeField] private CheckoutManager _checkout;

        [Tooltip("Сколько секунд уходит на обработку одного товара (скан + упаковка)")]
        [SerializeField] private float _processInterval = 1f;

        [Header("Звук (необязательно)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _scanSounds;

        private void Awake()
        {
            if (_checkout == null)
                _checkout = FindAnyObjectByType<CheckoutManager>();

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            StartCoroutine(ProcessLoop());
        }

        private IEnumerator ProcessLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_processInterval);

                if (_checkout == null || !_checkout.IsShiftActive) continue;

                ScannableItem item = _checkout.GetNextUnscannedItem();
                if (item == null) continue;

                item.MarkScanned();
                _checkout.RegisterScan(item);
                PlayRandomSound();

                yield return new WaitForSeconds(_processInterval);

                if (item == null) continue;

                item.MarkBagged();
                _checkout.RegisterBagged(item);
            }
        }

        private void PlayRandomSound()
        {
            if (_audioSource == null || _scanSounds == null || _scanSounds.Length == 0) return;

            AudioClip clip = _scanSounds[Random.Range(0, _scanSounds.Length)];
            if (clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}

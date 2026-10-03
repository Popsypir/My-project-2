using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePhone
{
    /// <summary>
    /// Один "слот" объявления-картинки на странице газеты. Расставляется вручную
    /// в редакторе (например 4 штуки) — заполняется кодом из NewspaperReaderController.
    /// </summary>
    public class NewspaperAdSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _adImage;

        [Tooltip("Необязательно - если номер не нарисован прямо на картинке")]
        [SerializeField] private TMP_Text _numberText;

        public void SetAd(NewspaperAdData ad)
        {
            gameObject.SetActive(true);

            if (_adImage != null)
                _adImage.sprite = ad.AdImage;

            if (_numberText != null)
                _numberText.text = ad.Number != null ? ad.Number.Number : "";
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }
    }
}

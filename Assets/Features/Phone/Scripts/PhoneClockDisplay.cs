using System.Collections;
using TMPro;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Настоящие часы на экране телефона - показывают реальное текущее время
    /// (System.DateTime.Now, часы на компьютере игрока), обновляются раз в
    /// секунду. Никак не связаны с внутриигровым днём (см. DayManager) -
    /// это именно реальное время, как на настоящем телефоне, для реализма.
    /// </summary>
    public class PhoneClockDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _clockText;
        [Tooltip("Формат времени - см. System.DateTime.ToString(format). По умолчанию \"ЧЧ:мм\"")]
        [SerializeField] private string _timeFormat = "HH:mm";

        private void OnEnable()
        {
            StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            while (true)
            {
                if (_clockText != null)
                    _clockText.text = System.DateTime.Now.ToString(_timeFormat);

                yield return new WaitForSeconds(1f);
            }
        }
    }
}

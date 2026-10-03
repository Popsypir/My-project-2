using TMPro;
using UnityEngine;

namespace GamePhone.Shop
{
    /// <summary>
    /// Просто выводит текущую статистику охранника (SecurityStats) в текстовые
    /// поля на экране. Обновляется сам при любом изменении - ничего опрашивать
    /// каждый кадр не нужно. Любое из полей можно не назначать, если конкретное
    /// число выводить не нужно.
    /// </summary>
    public class SecurityStatsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _caughtText;
        [SerializeField] private TMP_Text _escapedText;
        [SerializeField] private TMP_Text _returnedText;
        [SerializeField] private TMP_Text _lostText;

        private void OnEnable()
        {
            SecurityStats.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            SecurityStats.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_caughtText != null) _caughtText.text = $"{SecurityStats.ThievesCaught}";
            if (_escapedText != null) _escapedText.text = $"{SecurityStats.ThievesEscaped}";
            if (_returnedText != null) _returnedText.text = $"{SecurityStats.ItemsReturned}";
            if (_lostText != null) _lostText.text = $"{SecurityStats.ItemsLost}";
        }
    }
}

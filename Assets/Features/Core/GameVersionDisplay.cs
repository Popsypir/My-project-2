using TMPro;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Выводит версию самой игры (Application.version - берётся из Project Settings
    /// -> Player -> Version, а не версию редактора Unity) в текстовое поле.
    /// </summary>
    public class GameVersionDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _versionText;

        [Tooltip("{0} заменится на саму версию, например \"v{0}\"")]
        [SerializeField] private string _format = "{0}";

        private void Start()
        {
            if (_versionText != null)
                _versionText.text = string.Format(_format, Application.version);
        }
    }
}

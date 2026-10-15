using TMPro;
using UnityEngine;

namespace GamePhone.Warehouse
{
    /// <summary>
    /// Табло смены на почте: сколько осталось до конца дня, сколько посылок
    /// принято и выдано, а когда приехал фургон - сколько осталось на погрузку
    /// и сколько коробок уже влезло.
    ///
    /// Только показывает то, что считает PostOfficeShift, сам ни на что не влияет.
    /// Любое поле можно оставить пустым - та строка просто не будет обновляться.
    /// </summary>
    public class PostShiftHud : MonoBehaviour
    {
        [Header("Системы")]
        [Tooltip("Смена, у которой берутся все числа для табло")]
        [SerializeField] private PostOfficeShift _shift;
        [Tooltip("Нужен только для счётчика загруженных коробок")]
        [SerializeField] private Van _van;

        [Header("Строки")]
        [Tooltip("Время до конца смены, а после - до отъезда фургона")]
        [SerializeField] private TMP_Text _timerText;
        [Tooltip("Подпись над таймером: 'До конца смены' / 'Фургон уедет через'")]
        [SerializeField] private TMP_Text _timerCaption;
        [Tooltip("Счётчик принятых у посетителей посылок")]
        [SerializeField] private TMP_Text _acceptedText;
        [Tooltip("Счётчик выданных посылок")]
        [SerializeField] private TMP_Text _givenText;
        [Tooltip("Счётчик загруженных коробок, например 5/12. Виден только во время погрузки")]
        [SerializeField] private TMP_Text _loadedText;
        [Tooltip("Эти объекты включаются, только когда приехал фургон - например, рамка счётчика")]
        [SerializeField] private GameObject[] _loadingOnlyObjects;

        [Header("Подписи")]
        [Tooltip("Подпись над таймером, пока идёт рабочий день")]
        [SerializeField] private string _shiftCaption = "До конца смены";
        [Tooltip("Подпись над таймером, пока грузится фургон")]
        [SerializeField] private string _loadingCaption = "Фургон уедет через";
        [Tooltip("Что писать вместо времени, когда фургон ждёт без ограничения")]
        [SerializeField] private string _noTimerText = "Грузи фургон";

        private void Update()
        {
            if (_shift == null) return;

            bool loading = _shift.IsLoadingVan;

            string caption = loading ? _loadingCaption : _shiftCaption;
            if (_timerCaption != null) _timerCaption.text = caption;

            if (_timerText != null)
            {
                string time;
                if (loading)
                {
                    // Время погрузки не ограничено - отсчитывать нечего. Если
                    // подсказка не задана, оставляем нули: на табло должны быть
                    // только цифры, и они просто замирают на 00:00
                    float left = _shift.VanTimeLeft;
                    time = left > 0f || string.IsNullOrEmpty(_noTimerText)
                        ? Format(left)
                        : _noTimerText;
                }
                else time = Format(_shift.TimeLeft);

                // Подпись дописываем к цифрам, только если она вообще задана и
                // ей некуда больше деться. Пустая - значит на табло нужно
                // одно время, без слов
                _timerText.text = _timerCaption != null || string.IsNullOrEmpty(caption)
                    ? time
                    : caption + "\n" + time;
            }

            if (_acceptedText != null) _acceptedText.text = $"{_shift.ParcelsDelivered}";
            if (_givenText != null) _givenText.text = $"{_shift.ReceiversServed}";

            if (_loadedText != null && _van != null)
                // Вместимость не показываем: её нет, влезает сколько влезет
                _loadedText.text = _van.Count.ToString();

            if (_loadingOnlyObjects != null)
                foreach (GameObject go in _loadingOnlyObjects)
                    if (go != null && go.activeSelf != loading)
                        go.SetActive(loading);
        }

        private static string Format(float seconds)
        {
            // Округляем вверх: пока на часах 1 секунда, она ещё не истекла -
            // иначе таймер показывал бы 0 целую секунду до конца смены
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}

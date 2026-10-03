using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Один "известный" номер телефона в игре. Создаётся как ассет:
    /// правый клик в окне Project -> Create -> Game Phone -> Phone Number Entry.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Phone/Phone Number Entry", fileName = "NewPhoneNumber")]
    public class PhoneNumberEntry : ScriptableObject
    {
        [Tooltip("Номер, который игрок должен набрать на экране звонилки (только цифры, например 1234)")]
        public string Number;

        [Tooltip("Название сцены (Scene), в которой находится точка телепортации. Оставь ПУСТЫМ, если точка в той же сцене, где стоит стол/телефон")]
        public string SceneName;

        [Tooltip("Id точки телепортации в сцене (должен совпадать с Destination Id на TeleportDestination)")]
        public string DestinationId;

        [Tooltip("Просто для себя - что это за номер, чтобы не путаться в списке")]
        public string Label;

        [Tooltip("Необязательно - голосовая реплика/озвучка, которая проигрывается сразу после нажатия \"Позвонить\", " +
                 "перед тем как экран потемнеет и произойдёт телепортация. Если не задано - звонок сразу переходит " +
                 "к затемнению экрана")]
        public AudioClip CallAudio;

        [Tooltip("Необязательно - сценарий разговора с вопросами и ответами цифрами (собеседование и т.п.). " +
                 "Если задан - используется ВМЕСТО Call Audio")]
        public CallScript CallScript;

        [Tooltip("Номер \"Босса\": звонок сразу переносит игрока в указанную точку (Scene Name + Destination Id). " +
                 "Если выключено - это номер вакансии: звонок только оформляет на работу с завтрашнего утра, " +
                 "и работает лишь если номер есть в сегодняшней газете")]
        public bool InstantTeleport;
    }
}

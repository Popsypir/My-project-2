namespace GamePhone.Apps
{
    /// <summary>
    /// Титры - простой список текста внутри телефона, без анимации/бегущей
    /// строки. Сам текст просто вбивается в TMP_Text прямо в инспекторе
    /// (на объекте экрана этого приложения) - скрипту показывать нечего,
    /// он тут только чтобы приложение можно было завести в PhoneScreenManager
    /// и повесить на иконку (см. PhoneAppBase.OnOpen/OnClose).
    /// </summary>
    public class CreditsApp : PhoneAppBase
    {
    }
}

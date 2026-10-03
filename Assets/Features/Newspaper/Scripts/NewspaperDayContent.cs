using System.Collections.Generic;

namespace GamePhone
{
    /// <summary>
    /// Готовый "выпуск" газеты для конкретного дня — чистые данные, без UI.
    /// </summary>
    public class NewspaperDayContent
    {
        public string NewsText;
        public List<NewspaperAdData> Ads;
    }
}

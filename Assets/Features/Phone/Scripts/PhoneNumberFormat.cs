using System.Text;

namespace GamePhone
{
    /// <summary>
    /// Общая логика фильтрации/форматирования номера телефона - используется
    /// и в CallApp (набор номера), и в ContactsApp (ввод контакта), чтобы вид
    /// номера был одинаковым везде в игре.
    /// </summary>
    public static class PhoneNumberFormat
    {
        // "+X(XXX)XXX-XX-XX" - страна(1) + код(3) + номер(3+2+2) = 11 цифр
        public const int MaxDigits = 11;

        // Оставляет только цифры из произвольного текста, обрезает до maxDigits
        public static string ExtractDigits(string text, int maxDigits = MaxDigits)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsDigit(c))
                    sb.Append(c);
                if (sb.Length >= maxDigits) break;
            }
            return sb.ToString();
        }

        // Строит вид "+X(XXX)XXX-XX-XX" по мере набора, независимо от итоговой длины
        public static string FormatForDisplay(string digits)
        {
            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append('+');

            for (int i = 0; i < digits.Length; i++)
            {
                if (i == 1) sb.Append('(');
                else if (i == 4) sb.Append(')');
                else if (i == 7 || i == 9) sb.Append('-');

                sb.Append(digits[i]);
            }

            return sb.ToString();
        }
    }
}

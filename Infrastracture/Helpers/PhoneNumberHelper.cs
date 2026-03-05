namespace Abstractions.Helpers
{
    public static class PhoneNumberHelper
    {
        /// <summary>
        /// Извлекает последние 10 цифр из номера телефона или выбрасывает исключение, если цифр недостаточно.
        /// </summary>
        /// <param name="phoneNumber">Номер телефона в строковом формате.</param>
        /// <returns>Последние 10 цифр номера телефона.</returns>
        /// <exception cref="ArgumentException">Выбрасывается, если номер телефона не содержит достаточно цифр.</exception>
        public static string ExtractLastTenDigitsOrThrow(string phoneNumber)
        {
            if (!phoneNumber.All(c => char.IsDigit(c) || c == '+' || c == ' ' || c == '-' || c == '(' || c == ')'))
            {
                throw new ArgumentException(
                    "Номер телефона должен содержать только цифры и допустимые символы (пробелы, дефисы, скобки).",
                    nameof(phoneNumber));
            }

            var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 10)
            {
                throw new ArgumentException("Номер телефона должен содержать как минимум 10 цифр.", nameof(phoneNumber));
            }

            return digitsOnly[^10..];
        }

        public static string ConvertToWhatsappNumber(string phoneNumber)
        {
            const int russianNumberStart = 8;
            const int whatsappNumberStart = 7;
            var normalizedPhoneNumber = ExtractLastTenDigitsOrThrow(phoneNumber);
            return $"{whatsappNumberStart}{russianNumberStart}{normalizedPhoneNumber}";
        }
    }
}

using System;
using System.Globalization;

namespace ContestBot.Utils
{
    internal sealed class DateTimeParser
    {
        private static readonly string[] Formats =
        {
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy H:mm"
        };

        private readonly CultureInfo _culture;

        public DateTimeParser()
        {
            _culture = CultureInfo.GetCultureInfo("ru-RU");
        }

        public bool TryParse(string text, out DateTime dt)
        {
            dt = default(DateTime);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return DateTime.TryParseExact(
                text.Trim(),
                Formats,
                _culture,
                DateTimeStyles.AssumeLocal,
                out dt
            );
        }

        public DateTime? ParseNullable(string text)
        {
            DateTime dt;
            return TryParse(text, out dt) ? (DateTime?)dt : null;
        }
    }
}

using System.Globalization;

namespace UclAggregator.Clients;

internal static class DateParsing
{
    public static DateTimeOffset? Parse(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
}

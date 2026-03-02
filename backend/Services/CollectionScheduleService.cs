namespace BelfastBinsApi.Services;

public class CollectionScheduleService
{
    // Week Type 1 (anchor week): Blue A, Glass A, Black B, Brown B
    // Week Type 2 (alternate week): Blue B, Glass B, Black A, Brown A
    private static readonly Dictionary<(string BinType, string WeekCycle), int> CollectionWeekType =
        new(new BinWeekKeyComparer())
        {
            { ("Blue", "A"), 1 },
            { ("Blue", "B"), 2 },
            { ("Black", "A"), 2 },
            { ("Black", "B"), 1 },
            { ("Brown", "A"), 2 },
            { ("Brown", "B"), 1 },
            { ("Glass", "A"), 1 },
            { ("Glass", "B"), 2 },
        };

    private sealed class BinWeekKeyComparer : IEqualityComparer<(string BinType, string WeekCycle)>
    {
        public bool Equals((string BinType, string WeekCycle) x, (string BinType, string WeekCycle) y)
        {
            return string.Equals(x.BinType, y.BinType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.WeekCycle, y.WeekCycle, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string BinType, string WeekCycle) obj)
        {
            return HashCode.Combine(
                obj.BinType.ToUpperInvariant().GetHashCode(),
                obj.WeekCycle.ToUpperInvariant().GetHashCode());
        }
    }

    private readonly DateTime _anchorDate;
    private readonly int _anchorWeekType;
    private readonly ILogger<CollectionScheduleService> _logger;

    public CollectionScheduleService(IConfiguration configuration, ILogger<CollectionScheduleService> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("CollectionSchedule");
        _anchorDate = DateTime.Parse(section["AnchorDate"] ?? "2026-03-02", System.Globalization.CultureInfo.InvariantCulture).Date;
        _anchorWeekType = int.Parse(section["AnchorWeekType"] ?? "1");

        _logger.LogInformation("Collection schedule anchor: {AnchorDate} = Week Type {WeekType}", _anchorDate, _anchorWeekType);
    }

    /// <summary>
    /// Gets the week type (1 or 2) for a given date.
    /// </summary>
    public int GetWeekType(DateTime date)
    {
        // Align both dates to their ISO Monday to get consistent week boundaries
        var anchorMonday = _anchorDate.AddDays(-(((int)_anchorDate.DayOfWeek + 6) % 7));
        var dateMonday = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        var weeksDiff = (dateMonday - anchorMonday).Days / 7;

        return (weeksDiff % 2 == 0) ? _anchorWeekType : (_anchorWeekType == 1 ? 2 : 1);
    }

    /// <summary>
    /// Gets the next collection date for a given bin type, week cycle, and day of week.
    /// </summary>
    public DateTime? GetNextCollectionDate(string binType, string weekCycle, string dayOfWeek)
    {
        if (string.IsNullOrEmpty(binType) || string.IsNullOrEmpty(weekCycle) || string.IsNullOrEmpty(dayOfWeek))
        {
            return null;
        }

        var key = (binType, weekCycle);
        if (!CollectionWeekType.TryGetValue(key, out var targetWeekType))
        {
            _logger.LogWarning("Unknown bin type/week cycle combination: {BinType}/{WeekCycle}", binType, weekCycle);
            return null;
        }

        var targetDay = ParseDayOfWeek(dayOfWeek);
        if (targetDay == null)
        {
            _logger.LogWarning("Unknown day of week: {DayOfWeek}", dayOfWeek);
            return null;
        }

        var today = DateTime.Now.Date;

        // Find the next occurrence of the target day
        var daysUntilTarget = ((int)targetDay.Value - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0) daysUntilTarget = 7; // If today is the target day, go to next week

        var candidate = today.AddDays(daysUntilTarget);

        // Check if this candidate is in the right week type
        if (GetWeekType(candidate) != targetWeekType)
        {
            candidate = candidate.AddDays(7); // Move to the next week
        }

        return candidate;
    }

    /// <summary>
    /// Gets the display name for a bin type.
    /// </summary>
    public static string GetBinDisplayName(string binType)
    {
        return binType.ToUpperInvariant() switch
        {
            "BLACK" => "General Waste (Black Bin)",
            "BLUE" => "Recycling (Blue Bin)",
            "BROWN" => "Garden Waste (Brown Bin)",
            "GLASS" => "Glass Recycling (Glass Box)",
            _ => binType
        };
    }

    private static DayOfWeek? ParseDayOfWeek(string day)
    {
        return day.ToUpperInvariant() switch
        {
            "MONDAY" or "MON" => DayOfWeek.Monday,
            "TUESDAY" or "TUE" or "TUES" => DayOfWeek.Tuesday,
            "WEDNESDAY" or "WED" => DayOfWeek.Wednesday,
            "THURSDAY" or "THU" or "THUR" or "THURS" => DayOfWeek.Thursday,
            "FRIDAY" or "FRI" => DayOfWeek.Friday,
            "SATURDAY" or "SAT" => DayOfWeek.Saturday,
            "SUNDAY" or "SUN" => DayOfWeek.Sunday,
            _ => null
        };
    }
}

using System.Text.RegularExpressions;

namespace WhatBin.DataSeeder.Parsers;

/// <summary>
/// Normalizes the wildly inconsistent job codes from the staging table
/// into structured fields: Route, BinType, DayOfWeek, WeekCycle.
/// </summary>
public static class JobCodeNormalizer
{
    public record NormalizedJobCode(
        string Route,
        string BinType,
        string DayOfWeek,
        string WeekCycle);

    // Known day names/abbreviations → normalized short form
    private static readonly Dictionary<string, string> DayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Mon", "Mon" }, { "Monday", "Mon" },
        { "Tue", "Tue" }, { "Tues", "Tue" }, { "Tuesday", "Tue" },
        { "Wed", "Wed" }, { "Wednesday", "Wed" },
        { "Thu", "Thu" }, { "Thur", "Thu" }, { "Thurs", "Thu" }, { "Thursday", "Thu" },
        { "Fri", "Fri" }, { "Friday", "Fri" },
        { "Sat", "Sat" }, { "Saturday", "Sat" },
        { "Sun", "Sun" }, { "Sunday", "Sun" }
    };

    // Known bin type codes → normalized name
    private static readonly Dictionary<string, string> BinTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Blk", "Black" }, { "Black", "Black" },
        { "Blue", "Blue" },
        { "Brown", "Brown" },
        { "Green", "Green" },
        { "Glass", "Glass" }
    };

    // Regex: parenthetical bin type at end, e.g. "(Blue)", "(Brown Bin)", "(Brown Bins)", "(blue)", "(Blue" (unclosed)
    private static readonly Regex ParenBinTypeRegex = new(
        @"\(\s*(Blue|Brown|Black|Green|Glass)\s*(?:Bins?|Bin\s+Collection)?\s*\)?\s*(?:(?:Blue|Brown|Black|Green|Glass)\s+Bin\s+Collection)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex: trailing "Blue Bin Collection" / "Brown Bin Collection" without parens
    private static readonly Regex TrailingServiceRegex = new(
        @"\s*(?:Blue|Brown|Black|Green|Glass)\s+Bin\s+Collection\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for Glass format: "Glass N Day Wk? Cycle? - Region?"
    private static readonly Regex GlassFormatRegex = new(
        @"^Glass[\s-]+(\d+)[\s-]+(\w+)[\s-]*(?:Wk\s*)?([AB])?\s*(?:-\s*\w+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for route-number-day-week: "GC1-24-Mon-WkA" or "R1-153-Mon-WkA"
    private static readonly Regex RouteNumDayWeekRegex = new(
        @"^([A-Z]+\d+)-(\d+)-(\w+)-(Wk[AB]|[AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for route-number-day-week with bin type embedded: "R3-188-Blue-Wed-WkB"
    private static readonly Regex RouteNumBinDayWeekRegex = new(
        @"^([A-Z]+\d+)-(\d+)-(Black|Blue|Brown|Green|Blk|Glass)-(\w+)-(Wk[AB]|[AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for route-day-week without number: "R14-Mon-WkB" or "R15- Tue-WkB"
    private static readonly Regex RouteDayWeekRegex = new(
        @"^([A-Z]+\d+)-\s*(\w+)-(Wk[AB]|[AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for route-bintype-day-week (M-series, RA3, etc): "M1-Black-Fri-WkA" or "M1-Blue-Tues-A"
    private static readonly Regex RouteBinDayWeekRegex = new(
        @"^([A-Z]+\d*)\s*-\s*(Black|Blue|Brown|Green|Blk|Glass)\s*-\s*(\w+)\s*-\s*(Wk[AB]|Wk\s*[AB]|[AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for spaced format: "B3 - Blk - Mon A" or "E6 - Black - Thurs B"
    private static readonly Regex SpacedFormatRegex = new(
        @"^([A-Z]+\d*)\s*-\s*(Black|Blue|Brown|Green|Blk|Glass)\s*-\s*(\w+)\s+([AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for format with missing dash: "RA3 - Blue Fri A" or "RA3 - Black Mon A"
    private static readonly Regex MissingDashFormatRegex = new(
        @"^([A-Z]+\d*)\s*-\s*(Black|Blue|Brown|Green|Blk|Glass)\s+(\w+)\s+([AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for "M3-Black-Fri A" style (space before week instead of dash)
    private static readonly Regex MixedDashSpaceRegex = new(
        @"^([A-Z]+\d*)\s*-\s*(Black|Blue|Brown|Green|Blk|Glass)\s*-\s*(\w+)\s+([AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for "R16 Thur WkA (Blue)" - route then space then day then week, bin type in parens
    private static readonly Regex RouteSpaceDayWeekRegex = new(
        @"^([A-Z]+\d+)\s+(\w+)\s+(Wk[AB]|Wk\s+[AB])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for "GLB - Black - Wednesday" (missing week cycle)
    private static readonly Regex NoWeekCycleRegex = new(
        @"^([A-Z]+\d*)\s*-\s*(Black|Blue|Brown|Green|Blk|Glass)\s*-\s*(\w+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for "R14-Wed-B (Brown Bins)" - route-day-cycle with parenthetical
    private static readonly Regex RouteDayCycleRegex = new(
        @"^([A-Z]+\d+)-(\w+)-([AB])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Normalize a raw job code string (and optionally the service column) into structured fields.
    /// </summary>
    public static NormalizedJobCode? Normalize(string rawJobCode, string service = "")
    {
        if (string.IsNullOrWhiteSpace(rawJobCode))
            return null;

        var code = rawJobCode.Trim();

        // Step 1: Extract bin type from parenthetical if present, then strip it
        string? parenBinType = null;
        var parenMatch = ParenBinTypeRegex.Match(code);
        if (parenMatch.Success)
        {
            parenBinType = NormalizeBinType(parenMatch.Groups[1].Value);
            code = ParenBinTypeRegex.Replace(code, "").Trim();
        }

        // Step 2: Strip trailing "Blue Bin Collection" etc.
        code = TrailingServiceRegex.Replace(code, "").Trim();

        // Step 3: Extract bin type from Service column as fallback
        var serviceBinType = ExtractBinTypeFromService(service);

        // Step 4: Try each format pattern
        NormalizedJobCode? result;

        // Try Glass format first: "Glass 1 Fri Wk A - East" or "Glass-1-Mon-WkA-East"
        result = TryGlassFormat(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try route-number-bintype-day-week: "R3-188-Blue-Wed-WkB"
        result = TryRouteNumBinDayWeek(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try route-number-day-week: "GC1-24-Mon-WkA", "R1-153-Mon-WkA"
        result = TryRouteNumDayWeek(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try route-bintype-day-week (dashes): "M1-Black-Fri-WkA"
        result = TryRouteBinDayWeek(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try spaced format / mixed dash-space: "B3 - Blk - Mon A", "M3-Black-Fri A"
        result = TrySpacedFormat(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try missing dash format: "RA3 - Blue Fri A"
        result = TryMissingDashFormat(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try "R16 Thur WkA" (route space day week)
        result = TryRouteSpaceDayWeek(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try route-day-week without number: "R14-Mon-WkB"
        result = TryRouteDayWeek(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try route-day-cycle: "R14-Wed-B"
        result = TryRouteDayCycle(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try no-week-cycle format: "GLB - Black - Wednesday"
        result = TryNoWeekCycleFormat(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Try simple route-bintype: "QRT-Black"
        result = TrySimpleRouteBinType(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        // Fallback: just a route code like "B3" or "R15-"
        result = TryRouteOnly(code);
        if (result != null) return ApplyBinTypeOverride(result, parenBinType, serviceBinType);

        return null;
    }

    private static NormalizedJobCode? TryGlassFormat(string code)
    {
        // Handle both "Glass 1 Fri Wk A - East" and "Glass-1-Mon-WkA-East"
        var normalized = code.Replace("-", " ").Replace("  ", " ");

        var match = Regex.Match(normalized,
            @"^Glass\s+(\d+)\s+(\w+)(?:\s+Wk\s*([AB]))?(?:\s+\w+)?$",
            RegexOptions.IgnoreCase);

        if (!match.Success) return null;

        var routeNum = match.Groups[1].Value;
        var dayRaw = match.Groups[2].Value;
        var weekCycle = match.Groups[3].Success ? match.Groups[3].Value.ToUpper() : "";

        var day = NormalizeDay(dayRaw);
        if (string.IsNullOrEmpty(day)) return null; // dayRaw wasn't a valid day name

        return new NormalizedJobCode(
            Route: $"Glass {routeNum}",
            BinType: "Glass",
            DayOfWeek: day,
            WeekCycle: weekCycle);
    }

    private static NormalizedJobCode? TryRouteNumBinDayWeek(string code)
    {
        var match = RouteNumBinDayWeekRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        // Groups[2] is the numeric ID - skip it for route but keep for identification
        var binType = NormalizeBinType(match.Groups[3].Value);
        var day = NormalizeDay(match.Groups[4].Value);
        var weekCycle = NormalizeWeekCycle(match.Groups[5].Value);

        if (string.IsNullOrEmpty(day)) return null;

        return new NormalizedJobCode(route, binType, day, weekCycle);
    }

    private static NormalizedJobCode? TryRouteNumDayWeek(string code)
    {
        var match = RouteNumDayWeekRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        // Groups[2] is the numeric ID
        var day = NormalizeDay(match.Groups[3].Value);
        var weekCycle = NormalizeWeekCycle(match.Groups[4].Value);

        if (string.IsNullOrEmpty(day)) return null;

        // Bin type unknown from job code alone - will come from service column
        return new NormalizedJobCode(route, "", day, weekCycle);
    }

    private static NormalizedJobCode? TryRouteBinDayWeek(string code)
    {
        var match = RouteBinDayWeekRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var binType = NormalizeBinType(match.Groups[2].Value);
        var day = NormalizeDay(match.Groups[3].Value);
        var weekCycle = NormalizeWeekCycle(match.Groups[4].Value);

        if (string.IsNullOrEmpty(day)) return null;

        return new NormalizedJobCode(route, binType, day, weekCycle);
    }

    private static NormalizedJobCode? TrySpacedFormat(string code)
    {
        // Matches both "B3 - Blk - Mon A" and "M3-Black-Fri A"
        var match = SpacedFormatRegex.Match(code);
        if (!match.Success)
            match = MixedDashSpaceRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var binType = NormalizeBinType(match.Groups[2].Value);
        var day = NormalizeDay(match.Groups[3].Value);
        var weekCycle = match.Groups[4].Value.ToUpper();

        return new NormalizedJobCode(route, binType, day, weekCycle);
    }

    private static NormalizedJobCode? TryMissingDashFormat(string code)
    {
        var match = MissingDashFormatRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var binType = NormalizeBinType(match.Groups[2].Value);
        var day = NormalizeDay(match.Groups[3].Value);
        var weekCycle = match.Groups[4].Value.ToUpper();

        return new NormalizedJobCode(route, binType, day, weekCycle);
    }

    private static NormalizedJobCode? TryRouteSpaceDayWeek(string code)
    {
        var match = RouteSpaceDayWeekRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var day = NormalizeDay(match.Groups[2].Value);
        var weekCycle = NormalizeWeekCycle(match.Groups[3].Value);

        if (string.IsNullOrEmpty(day)) return null;

        return new NormalizedJobCode(route, "", day, weekCycle);
    }

    private static NormalizedJobCode? TryRouteDayWeek(string code)
    {
        var match = RouteDayWeekRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var day = NormalizeDay(match.Groups[2].Value);
        var weekCycle = NormalizeWeekCycle(match.Groups[3].Value);

        if (string.IsNullOrEmpty(day)) return null;

        return new NormalizedJobCode(route, "", day, weekCycle);
    }

    private static NormalizedJobCode? TryRouteDayCycle(string code)
    {
        var match = RouteDayCycleRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var day = NormalizeDay(match.Groups[2].Value);
        var weekCycle = match.Groups[3].Value.ToUpper();

        if (string.IsNullOrEmpty(day)) return null;

        return new NormalizedJobCode(route, "", day, weekCycle);
    }

    private static NormalizedJobCode? TryNoWeekCycleFormat(string code)
    {
        var match = NoWeekCycleRegex.Match(code);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var binType = NormalizeBinType(match.Groups[2].Value);
        var day = NormalizeDay(match.Groups[3].Value);

        if (string.IsNullOrEmpty(day)) return null;

        return new NormalizedJobCode(route, binType, day, "");
    }

    private static NormalizedJobCode? TrySimpleRouteBinType(string code)
    {
        // "QRT-Black" or similar
        var match = Regex.Match(code, @"^([A-Z]+\d*)\s*-\s*(Black|Blue|Brown|Green|Blk|Glass)$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var route = match.Groups[1].Value;
        var binType = NormalizeBinType(match.Groups[2].Value);

        return new NormalizedJobCode(route, binType, "", "");
    }

    private static NormalizedJobCode? TryRouteOnly(string code)
    {
        // Just a route code: "B3", "R15-", etc.
        var cleaned = code.TrimEnd('-', ' ');
        if (Regex.IsMatch(cleaned, @"^[A-Z]+\d*$", RegexOptions.IgnoreCase))
        {
            return new NormalizedJobCode(cleaned, "", "", "");
        }
        return null;
    }

    private static NormalizedJobCode ApplyBinTypeOverride(NormalizedJobCode result, string? parenBinType, string serviceBinType)
    {
        // Priority: parenthetical > job code > service column
        var binType = result.BinType;

        if (!string.IsNullOrEmpty(parenBinType))
        {
            binType = parenBinType;
        }
        else if (string.IsNullOrEmpty(binType) && !string.IsNullOrEmpty(serviceBinType))
        {
            binType = serviceBinType;
        }

        if (binType == result.BinType) return result;

        return result with { BinType = binType };
    }

    private static string NormalizeDay(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return DayMap.TryGetValue(raw, out var normalized) ? normalized : "";
    }

    private static string NormalizeBinType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return BinTypeMap.TryGetValue(raw, out var normalized) ? normalized : raw;
    }

    private static string NormalizeWeekCycle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // Handle "WkA", "WkB", "Wk A", "Wk B", "WK B", "A", "B"
        var cleaned = raw.Replace("Wk", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("WK", "", StringComparison.OrdinalIgnoreCase)
                        .Trim()
                        .ToUpper();
        return cleaned is "A" or "B" ? cleaned : "";
    }

    private static string ExtractBinTypeFromService(string service)
    {
        if (string.IsNullOrWhiteSpace(service)) return "";

        if (service.Contains("Black", StringComparison.OrdinalIgnoreCase)) return "Black";
        if (service.Contains("Blue", StringComparison.OrdinalIgnoreCase)) return "Blue";
        if (service.Contains("Brown", StringComparison.OrdinalIgnoreCase)) return "Brown";
        if (service.Contains("Green", StringComparison.OrdinalIgnoreCase)) return "Green";
        if (service.Contains("Glass", StringComparison.OrdinalIgnoreCase)) return "Glass";

        return "";
    }
}

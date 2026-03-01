using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using WhatBin.DataSeeder.Models;

namespace WhatBin.DataSeeder.Parsers;

public static class BinSchedulePdfParser
{
    // Matches job codes like "B3 - Blk - Mon A", "E7 - Blue - Fri B", "E8 - Blue - Wed A"
    private static readonly Regex JobCodePattern = new(
        @"^[A-Z]\d+\s*-\s*\w+\s*-\s*\w+\s+[AB]$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Day abbreviation mapping from PDF format to short format
    private static readonly Dictionary<string, string> DayMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Mon", "Mon" },
        { "Monday", "Mon" },
        { "Tue", "Tue" },
        { "Tues", "Tue" },
        { "Tuesday", "Tue" },
        { "Wed", "Wed" },
        { "Wednesday", "Wed" },
        { "Thu", "Thu" },
        { "Thurs", "Thu" },
        { "Thursday", "Thu" },
        { "Fri", "Fri" },
        { "Friday", "Fri" },
        { "Sat", "Sat" },
        { "Saturday", "Sat" },
        { "Sun", "Sun" },
        { "Sunday", "Sun" }
    };

    // Bin type mapping from job code abbreviation
    private static readonly Dictionary<string, string> BinTypeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Blk", "Black" },
        { "Blue", "Blue" },
        { "Brown", "Brown" },
        { "Green", "Green" }
    };

    public static IEnumerable<BinSchedule> ParsePdf(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        var records = new List<BinSchedule>();

        using var document = PdfDocument.Open(pdfPath);

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            // PdfPig returns all text concatenated; we need to split by lines
            // Use the page's words grouped by Y coordinate to reconstruct lines
            var words = page.GetWords().ToList();
            if (!words.Any()) continue;

            // Group words by their approximate Y position (within 2 units tolerance)
            var lineGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key) // Top to bottom (PDF Y is bottom-up)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
                .ToList();

            ParseLines(lineGroups, records);
        }

        return records;
    }

    private static void ParseLines(List<string> lines, List<BinSchedule> records)
    {
        // The PDF has repeating groups of 3 lines:
        // 1. JOB_CODE (e.g., "B3 - Blk - Mon A")
        // 2. SERVICE (e.g., "Black Bin Collection")
        // 3. Full Address (e.g., "1, avonorr drive, belfast, down, bt5 5aj")
        //
        // We skip headers like "JOB_CODE", "SERVICE", "Full Address"

        var filteredLines = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Where(l => l != "JOB_CODE" && l != "SERVICE" && l != "Full Address")
            .ToList();

        int i = 0;
        while (i < filteredLines.Count)
        {
            var line = filteredLines[i].Trim();

            // Look for a job code line
            if (IsJobCode(line))
            {
                var jobCode = line;

                // Next line should be SERVICE (skip it)
                // Next after that should be the address
                if (i + 2 < filteredLines.Count)
                {
                    var serviceLine = filteredLines[i + 1].Trim();
                    var addressLine = filteredLines[i + 2].Trim();

                    // Verify serviceLine looks like a service description
                    if (serviceLine.Contains("Bin Collection", StringComparison.OrdinalIgnoreCase))
                    {
                        var schedule = ParseRecord(jobCode, addressLine);
                        if (schedule != null)
                        {
                            records.Add(schedule);
                        }
                        i += 3;
                        continue;
                    }
                }
            }

            i++;
        }
    }

    private static bool IsJobCode(string line)
    {
        return JobCodePattern.IsMatch(line);
    }

    private static BinSchedule? ParseRecord(string jobCode, string fullAddress)
    {
        // Parse job code: "B3 - Blk - Mon A" → route="B3", binTypeCode="Blk", day="Mon", weekCycle="A"
        var jobParts = jobCode.Split('-', StringSplitOptions.TrimEntries);
        if (jobParts.Length < 3) return null;

        var route = jobParts[0]; // "B3"
        var binTypeCode = jobParts[1]; // "Blk"

        // The last part contains day and week cycle: "Mon A"
        var dayAndCycle = jobParts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (dayAndCycle.Length < 2) return null;

        var dayRaw = dayAndCycle[0]; // "Mon"
        var weekCycle = dayAndCycle[1]; // "A"

        // Map day to normalized short form
        if (!DayMappings.TryGetValue(dayRaw, out var dayOfWeek))
        {
            dayOfWeek = dayRaw; // Fallback to raw value
        }

        // Map bin type
        if (!BinTypeMappings.TryGetValue(binTypeCode, out var binType))
        {
            binType = binTypeCode; // Fallback to raw value
        }

        // Parse address: "1, avonorr drive, belfast, down, bt5 5aj"
        var addressParts = fullAddress.Split(',', StringSplitOptions.TrimEntries);
        if (addressParts.Length < 4) return null;

        // Extract house number (may contain suffix like "1a")
        var houseNumberRaw = addressParts[0].Trim();
        var (houseNumber, houseSuffix) = ParseHouseNumber(houseNumberRaw);

        // The street is the second part
        var street = addressParts.Length > 1 ? addressParts[1].Trim() : "";

        // City and county
        var city = addressParts.Length > 2 ? addressParts[2].Trim() : "";
        var county = addressParts.Length > 3 ? addressParts[3].Trim() : "";

        // Postcode is the last part
        var postcode = addressParts.Length > 4 ? addressParts[^1].Trim() : "";

        // Normalize
        var postcodeNormalized = postcode.Replace(" ", "").ToUpper();
        var streetNormalized = street.ToUpper().Trim();

        return new BinSchedule
        {
            Route = route,
            BinType = binType,
            DayOfWeek = dayOfWeek,
            WeekCycle = weekCycle,
            HouseNumber = houseNumber.ToUpper(),
            HouseSuffix = houseSuffix.ToUpper(),
            Street = street,
            StreetNormalized = streetNormalized,
            City = city,
            County = county,
            Postcode = postcode,
            PostcodeNormalized = postcodeNormalized,
            FullAddress = fullAddress
        };
    }

    private static (string Number, string Suffix) ParseHouseNumber(string raw)
    {
        // Split "12a" into ("12", "a"), or "12" into ("12", "")
        var match = Regex.Match(raw, @"^(\d+)([a-zA-Z]*)$");
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return (raw, "");
    }
}

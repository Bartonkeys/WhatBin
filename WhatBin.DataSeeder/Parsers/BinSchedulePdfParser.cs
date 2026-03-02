using System.Text.RegularExpressions;
using BelfastBinsApi.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace WhatBin.DataSeeder.Parsers;

public static class BinSchedulePdfParser
{
    // Matches section headers like "Glass 4 Monday Wk A - EAST" (without address) to skip them
    private static readonly Regex SectionHeaderPattern = new(
        @"^[A-Za-z]+\s+\d+\s+\w+\s+Wk\s+[AB]\s*-\s*[A-Z]+$",
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
        { "Black", "Black" },
        { "Blue", "Blue" },
        { "Brown", "Brown" },
        { "Green", "Green" },
        { "Glass", "Glass" }
    };

    public static IEnumerable<BinSchedule> ParsePdf(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        var records = new List<BinSchedule>();

        using var document = PdfDocument.Open(pdfPath);

        var pages = document.GetPages();

        foreach (var page in pages)
        {
            var words = page.GetWords().ToList();
            if (!words.Any()) continue;

            // Group words by their approximate Y position (same line)
            var lineGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key) // Top to bottom
                .Select(g => g.OrderBy(w => w.BoundingBox.Left).ToList())
                .ToList();

            ParseLinesByPosition(lineGroups, records);
        }

        return records;
    }

    public static IEnumerable<StagingBinSchedule> ParsePdfIntoStaging(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        var records = new List<StagingBinSchedule>();

        using var document = PdfDocument.Open(pdfPath);

        var pages = document.GetPages();

        foreach (var page in pages)
        {
            var words = page.GetWords().ToList();
            if (!words.Any()) continue;

            // Group words by their approximate Y position (same line)
            var lineGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key) // Top to bottom
                .Select(g => g.OrderBy(w => w.BoundingBox.Left).ToList())
                .ToList();

            ParseLinesByPositionIntoStaging(lineGroups, records);
        }

        return records;
    }

    private static void ParseLinesByPositionIntoStaging(List<List<Word>> lineGroups, List<StagingBinSchedule> records)
    {
        // Skip header lines by looking for common header text
        var dataLines = lineGroups
            .Where(line => line.Any())
            .Where(line => !IsHeaderLine(line))
            .ToList();

        foreach (var words in dataLines)
        {
            try
            {
                // Analyze the X positions to determine column boundaries
                // Typical format has 3 columns: JOB_CODE | SERVICE | Full Address
                var columns = SplitIntoColumns(words);

                if (columns.Count < 3)
                {
                    Console.WriteLine($"Skipping line with insufficient columns: {string.Join(" ", words.Select(w => w.Text))}");
                    continue;
                }

                var jobCodeText = string.Join(" ", columns[0].Select(w => w.Text)).Trim();
                var serviceText = string.Join(" ", columns[1].Select(w => w.Text)).Trim();
                var addressText = string.Join(" ", columns[2].Select(w => w.Text)).Trim();

                var stagingBinScheule = new StagingBinSchedule
                {
                    JobCode = jobCodeText,
                    Service = serviceText,
                    FullAddress = addressText
                };

                records.Add(stagingBinScheule);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing line: {ex.Message}");
            }
        }
    }

    private static void ParseLinesByPosition(List<List<Word>> lineGroups, List<BinSchedule> records)
    {
        // Skip header lines by looking for common header text
        var dataLines = lineGroups
            .Where(line => line.Any())
            .Where(line => !IsHeaderLine(line))
            .ToList();

        foreach (var words in dataLines)
        {
            try
            {
                // Analyze the X positions to determine column boundaries
                // Typical format has 3 columns: JOB_CODE | SERVICE | Full Address
                var columns = SplitIntoColumns(words);

                if (columns.Count < 3)
                {
                    Console.WriteLine($"Skipping line with insufficient columns: {string.Join(" ", words.Select(w => w.Text))}");
                    continue;
                }

                var jobCodeText = string.Join(" ", columns[0].Select(w => w.Text)).Trim();
                var serviceText = string.Join(" ", columns[1].Select(w => w.Text)).Trim();
                var addressText = string.Join(" ", columns[2].Select(w => w.Text)).Trim();

                // Validate this is a collection record
                if (!serviceText.Contains("Collection", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Skipping non-collection line: {jobCodeText} | {serviceText}");
                    continue;
                }

                // Parse the job code
                var schedule = ParseJobCodeAndAddress(jobCodeText, addressText);
                if (schedule != null)
                {
                    records.Add(schedule);
                }
                else
                {
                    Console.WriteLine($"Failed to parse: {jobCodeText} | {addressText}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing line: {ex.Message}");
            }
        }
    }

    private static bool IsHeaderLine(List<Word> words)
    {
        var lineText = string.Join(" ", words.Select(w => w.Text)).Trim();

        if (string.IsNullOrWhiteSpace(lineText)) return true;
        if (lineText.Equals("JOB_CODE", StringComparison.OrdinalIgnoreCase)) return true;
        if (lineText.Equals("SERVICE", StringComparison.OrdinalIgnoreCase)) return true;
        if (lineText.Equals("Full Address", StringComparison.OrdinalIgnoreCase)) return true;
        if (lineText.StartsWith("JOB_CODE", StringComparison.OrdinalIgnoreCase)) return true;
        if (SectionHeaderPattern.IsMatch(lineText)) return true;

        return false;
    }

    private static List<List<Word>> SplitIntoColumns(List<Word> words)
    {
        if (!words.Any()) return new List<List<Word>>();

        // Calculate gaps between words to identify column boundaries
        var gaps = new List<(int Index, double Gap)>();
        for (int i = 0; i < words.Count - 1; i++)
        {
            var currentWordRight = words[i].BoundingBox.Right;
            var nextWordLeft = words[i + 1].BoundingBox.Left;
            var gap = nextWordLeft - currentWordRight;
            gaps.Add((i, gap));
        }

        // Find the largest gaps (likely column separators)
        // We expect 2 major gaps for 3 columns
        var significantGaps = gaps
            .OrderByDescending(g => g.Gap)
            .Take(2)
            .OrderBy(g => g.Index)
            .ToList();

        if (!significantGaps.Any())
        {
            // No significant gaps, treat as single column
            return new List<List<Word>> { words };
        }

        // Split words into columns based on gap positions
        var columns = new List<List<Word>>();
        var currentColumn = new List<Word>();
        var gapIndices = significantGaps.Select(g => g.Index).ToHashSet();

        for (int i = 0; i < words.Count; i++)
        {
            currentColumn.Add(words[i]);

            if (gapIndices.Contains(i))
            {
                columns.Add(currentColumn);
                currentColumn = new List<Word>();
            }
        }

        // Add the last column
        if (currentColumn.Any())
        {
            columns.Add(currentColumn);
        }

        return columns;
    }

    private static BinSchedule? ParseJobCodeAndAddress(string jobCodeText, string addressText)
    {
        // Job code format examples:
        // "B3 - Blk - Mon A"
        // "E7-Blue-Thursday-A"
        // "RA3 - Blue Wed B"
        // "GLB - Brown - Friday A"

        // Strategy: Split by hyphen, then parse the components
        var parts = jobCodeText.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
        {
            parts = jobCodeText.Split(' ', StringSplitOptions.TrimEntries);
        }

        var route = parts[0].Trim(); // "B3", "E7", "RA3", "GLB"
        var binTypeCode = parts[1].Trim(); // "Blk", "Blue", "Brown"

        // The remaining parts contain day and week cycle
        // They might be in one part "Mon A" or split "Thursday" and "A"
        string dayRaw;
        string weekCycle;

        switch (parts.Length)
        {
            case 3:
            {
                // Format: "B3 - Blk - Mon A" → parts = ["B3", "Blk", "Mon A"]
                var lastPart = parts[2].Trim();
                var dayAndCycle = lastPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (dayAndCycle.Length)
                {
                    case 1:
                        dayRaw = dayAndCycle[0];
                        weekCycle = lastPart[^1].ToString(); // Last character as week cycle
                        break;
                    case >= 2:
                        dayRaw = dayAndCycle[0];
                        weekCycle = dayAndCycle[1];
                        break;
                    default:
                        Console.WriteLine($"Cannot parse day/cycle from: {lastPart}");
                        return null;
                }

                break;
            }
            case >= 4:
                // Format: "E7-Blue-Thursday-A" → parts = ["E7", "Blue", "Thursday", "A"]
                dayRaw = parts[2].Trim();
                weekCycle = parts[3].Trim();
                break;
            default:
                return null;
        }

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

        return CreateBinSchedule(route, binType, dayOfWeek, weekCycle, addressText);
    }

    private static BinSchedule? CreateBinSchedule(string route, string binType, string dayOfWeek, string weekCycle, string fullAddress)
    {
        // Parse address: "1, avonorr drive, belfast, down, bt5 5aj"
        var addressParts = fullAddress.Split(',', StringSplitOptions.TrimEntries);
        if (addressParts.Length < 3) return null;

        // Extract house number (may contain suffix like "1a" or text like "apartment 1")
        var houseNumberRaw = addressParts[0].Trim();
        var (houseNumber, houseSuffix) = ParseHouseNumber(houseNumberRaw);

        // The street is the second part
        var street = addressParts.Length > 1 ? addressParts[1].Trim() : "";

        // City is always the third part
        var city = addressParts.Length > 2 ? addressParts[2].Trim() : "";

        // For 5+ parts: house, street, city, county, postcode
        // For 4 parts: house, street, city, postcode (no county)
        var county = addressParts.Length > 4 ? addressParts[3].Trim() : "";
        var postcode = addressParts.Length > 3 ? addressParts[^1].Trim() : "";

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

        // Handle "apartment 1" or other text-based house identifiers
        return (raw, "");
    }
}
using System.Text.RegularExpressions;
using BelfastBinsApi.Data;
using BelfastBinsApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatBin.DataSeeder.Parsers;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("Error: ConnectionStrings:DefaultConnection is not configured in appsettings.json");
    return 1;
}

var optionsBuilder = new DbContextOptionsBuilder<BinDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var context = new BinDbContext(optionsBuilder.Options);

// Ensure database and tables exist
Console.WriteLine("Ensuring database schema exists...");
await context.Database.EnsureCreatedAsync();

// Determine which command to run
var command = args.Length > 0 ? args[0].ToLower() : "help";

return command switch
{
    "import" => await ImportPdfsToStaging(args, context),
    "process" => await ProcessStagingToBinSchedules(context),
    "all" => await RunAll(args, context),
    _ => ShowHelp()
};

// ── Import PDFs into staging table ──────────────────────────────────────────
static async Task<int> ImportPdfsToStaging(string[] args, BinDbContext context)
{
    var pdfFolder = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "pdfs");

    if (!Directory.Exists(pdfFolder))
    {
        Console.Error.WriteLine($"Error: PDF folder not found: {pdfFolder}");
        return 1;
    }

    var pdfFiles = Directory.GetFiles(pdfFolder, "*.pdf");
    if (pdfFiles.Length == 0)
    {
        Console.Error.WriteLine($"Error: No PDF files found in {pdfFolder}");
        return 1;
    }

    Console.WriteLine($"Found {pdfFiles.Length} PDF file(s) in {pdfFolder}");

var optionsBuilder = new DbContextOptionsBuilder<BinDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var context = new BinDbContext(optionsBuilder.Options);

// Ensure database and tables exist
Console.WriteLine("Ensuring database schema exists...");
await context.Database.EnsureCreatedAsync();

// Clear existing staging data
Console.WriteLine("Clearing existing bin stagingschedule data...");
var stagingCount = await context.StagingBinSchedules.CountAsync();
if (stagingCount > 0)
{
    Console.WriteLine($"  Removing {stagingCount:N0} existing records...");
    context.StagingBinSchedules.RemoveRange(context.StagingBinSchedules);
    await context.SaveChangesAsync();
}

// Clear existing data
Console.WriteLine("Clearing existing bin schedule data...");
var existingCount = await context.BinSchedules.CountAsync();
if (existingCount > 0)
{
    Console.WriteLine($"  Removing {existingCount:N0} existing records...");
    context.BinSchedules.RemoveRange(context.BinSchedules);
    await context.SaveChangesAsync();
}

var totalRecords = 0;

    foreach (var pdfFile in pdfFiles)
    {
        var fileName = Path.GetFileName(pdfFile);
        Console.WriteLine($"\nProcessing: {fileName}");

        try
        {
            var schedules = BinSchedulePdfParser.ParsePdfIntoStaging(pdfFile).ToList();
            Console.WriteLine($"  Parsed {schedules.Count:N0} records");

            if (schedules.Count == 0)
            {
                Console.WriteLine("  Warning: No records parsed from this file");
                continue;
            }

            const int batchSize = 1000;
            for (var i = 0; i < schedules.Count; i += batchSize)
            {
                var batch = schedules.Skip(i).Take(batchSize).ToList();
                context.StagingBinSchedules.AddRange(batch);
                await context.SaveChangesAsync();

                foreach (var entry in context.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                var progress = Math.Min(i + batchSize, schedules.Count);
                Console.Write($"\r  Inserted {progress:N0} / {schedules.Count:N0} records");
            }

            Console.WriteLine();
            totalRecords += schedules.Count;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Error processing {fileName}: {ex.Message}");

            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    Console.WriteLine($"\nImport complete. Total staging records inserted: {totalRecords:N0}");
    return 0;
}

// ── Process staging records into BinSchedules ───────────────────────────────
static async Task<int> ProcessStagingToBinSchedules(BinDbContext context)
{
    var stagingCount = await context.StagingBinSchedules.CountAsync();
    if (stagingCount == 0)
    {
        Console.Error.WriteLine("Error: No records in staging table. Run 'import' first.");
        return 1;
    }

    Console.WriteLine($"Processing {stagingCount:N0} staging records...");

    // Clear existing BinSchedules
    var existingCount = await context.BinSchedules.CountAsync();
    if (existingCount > 0)
    {
        Console.WriteLine($"  Clearing {existingCount:N0} existing BinSchedule records...");
        await context.BinSchedules.ExecuteDeleteAsync();
    }

    var totalInserted = 0;
    var totalSkipped = 0;
    var failedJobCodes = new Dictionary<string, int>();
    var normalizedJobCodes = new Dictionary<string, int>();

    // Process in batches to avoid loading all staging records at once
    const int readBatchSize = 5000;
    const int writeBatchSize = 1000;
    var offset = 0;

    while (true)
    {
        var stagingBatch = await context.StagingBinSchedules
            .OrderBy(s => s.Id)
            .Skip(offset)
            .Take(readBatchSize)
            .ToListAsync();

        if (stagingBatch.Count == 0) break;

        var binSchedules = new List<BinSchedule>();

        foreach (var staging in stagingBatch)
        {
            var normalized = JobCodeNormalizer.Normalize(staging.JobCode, staging.Service);

            if (normalized == null)
            {
                totalSkipped++;
                var key = staging.JobCode;
                failedJobCodes[key] = failedJobCodes.GetValueOrDefault(key) + 1;
                continue;
            }

            // Track normalized results for summary
            var normalizedKey = $"{normalized.Route}|{normalized.BinType}|{normalized.DayOfWeek}|{normalized.WeekCycle}";
            normalizedJobCodes[normalizedKey] = normalizedJobCodes.GetValueOrDefault(normalizedKey) + 1;

            // Parse address
            var address = ParseAddress(staging.FullAddress);
            if (address == null)
            {
                totalSkipped++;
                continue;
            }

            binSchedules.Add(new BinSchedule
            {
                Route = normalized.Route,
                BinType = normalized.BinType,
                DayOfWeek = normalized.DayOfWeek,
                WeekCycle = normalized.WeekCycle,
                HouseNumber = address.HouseNumber,
                HouseSuffix = address.HouseSuffix,
                Street = address.Street,
                StreetNormalized = address.StreetNormalized,
                City = address.City,
                County = address.County,
                Postcode = address.Postcode,
                PostcodeNormalized = address.PostcodeNormalized,
                FullAddress = staging.FullAddress
            });
        }

        // Insert the processed batch
        for (var i = 0; i < binSchedules.Count; i += writeBatchSize)
        {
            var writeBatch = binSchedules.Skip(i).Take(writeBatchSize).ToList();
            context.BinSchedules.AddRange(writeBatch);
            await context.SaveChangesAsync();

            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        totalInserted += binSchedules.Count;
        offset += readBatchSize;
        Console.Write($"\r  Processed {offset:N0} / {stagingCount:N0} staging records → {totalInserted:N0} inserted");
    }

    Console.WriteLine();
    Console.WriteLine($"\nProcessing complete:");
    Console.WriteLine($"  Total staging records: {stagingCount:N0}");
    Console.WriteLine($"  Successfully processed: {totalInserted:N0}");
    Console.WriteLine($"  Skipped: {totalSkipped:N0}");

    if (failedJobCodes.Count > 0)
    {
        Console.WriteLine($"\n  Failed job codes ({failedJobCodes.Count} distinct):");
        foreach (var (jobCode, count) in failedJobCodes.OrderByDescending(x => x.Value).Take(20))
        {
            Console.WriteLine($"    [{count:N0} records] {jobCode}");
        }
        if (failedJobCodes.Count > 20)
        {
            Console.WriteLine($"    ... and {failedJobCodes.Count - 20} more");
        }
    }

    // Print summary of normalized results
    Console.WriteLine($"\n  Distinct normalized job codes: {normalizedJobCodes.Count}");
    var binTypeSummary = normalizedJobCodes
        .GroupBy(x => x.Key.Split('|')[1])
        .Select(g => new { BinType = g.Key == "" ? "(unknown)" : g.Key, Count = g.Sum(x => x.Value) })
        .OrderByDescending(x => x.Count);
    Console.WriteLine("  By bin type:");
    foreach (var bt in binTypeSummary)
    {
        Console.WriteLine($"    {bt.BinType}: {bt.Count:N0} records");
    }

    return 0;
}

// ── Run both import and process ─────────────────────────────────────────────
static async Task<int> RunAll(string[] args, BinDbContext context)
{
    var result = await ImportPdfsToStaging(args, context);
    if (result != 0) return result;

    Console.WriteLine("\n" + new string('─', 60) + "\n");

    return await ProcessStagingToBinSchedules(context);
}

// ── Address parser ──────────────────────────────────────────────────────────
static ParsedAddress? ParseAddress(string fullAddress)
{
    if (string.IsNullOrWhiteSpace(fullAddress)) return null;

    var parts = fullAddress.Split(',', StringSplitOptions.TrimEntries);
    if (parts.Length < 3) return null;

    var houseNumberRaw = parts[0].Trim();
    var (houseNumber, houseSuffix) = ParseHouseNumber(houseNumberRaw);

    var street = parts.Length > 1 ? parts[1].Trim() : "";
    var city = parts.Length > 2 ? parts[2].Trim() : "";

    // For 5+ parts: house, street, city, county, postcode
    // For 4 parts: house, street, city, postcode (no county)
    var county = parts.Length > 4 ? parts[3].Trim() : "";
    var postcode = parts.Length > 3 ? parts[^1].Trim() : "";

    var postcodeNormalized = postcode.Replace(" ", "").ToUpper();
    var streetNormalized = street.ToUpper().Trim();

    return new ParsedAddress(
        HouseNumber: houseNumber.ToUpper(),
        HouseSuffix: houseSuffix.ToUpper(),
        Street: street,
        StreetNormalized: streetNormalized,
        City: city,
        County: county,
        Postcode: postcode,
        PostcodeNormalized: postcodeNormalized);
}

static (string Number, string Suffix) ParseHouseNumber(string raw)
{
    var match = Regex.Match(raw, @"^(\d+)([a-zA-Z]*)$");
    if (match.Success)
    {
        return (match.Groups[1].Value, match.Groups[2].Value);
    }
    return (raw, "");
}

static int ShowHelp()
{
    Console.WriteLine("WhatBin.DataSeeder - Bin collection schedule data pipeline");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  import [pdf-folder]  Import PDFs into staging table");
    Console.WriteLine("  process              Normalize staging records into BinSchedules");
    Console.WriteLine("  all [pdf-folder]     Run both import and process");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run import ./pdfs");
    Console.WriteLine("  dotnet run process");
    Console.WriteLine("  dotnet run all ./pdfs");
    return 0;
}

record ParsedAddress(
    string HouseNumber,
    string HouseSuffix,
    string Street,
    string StreetNormalized,
    string City,
    string County,
    string Postcode,
    string PostcodeNormalized);

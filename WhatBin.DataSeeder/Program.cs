using BelfastBinsApi.Data;
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

// Determine the PDF folder path from args or default to ./pdfs
var pdfFolder = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "pdfs");

if (!Directory.Exists(pdfFolder))
{
    Console.Error.WriteLine($"Error: PDF folder not found: {pdfFolder}");
    Console.Error.WriteLine("Usage: dotnet run [path-to-pdf-folder]");
    Console.Error.WriteLine("  Place PDF files in a 'pdfs' folder next to the executable, or pass the folder path as an argument.");
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

        // Insert in batches for performance
        const int batchSize = 1000;
        for (var i = 0; i < schedules.Count; i += batchSize)
        {
            var batch = schedules.Skip(i).Take(batchSize).ToList();
            context.StagingBinSchedules.AddRange(batch);
            await context.SaveChangesAsync();

            // Detach tracked entities to free memory
            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            var progress = Math.Min(i + batchSize, schedules.Count);
            Console.Write($"\r  Inserted {progress:N0} / {schedules.Count:N0} records");
        }

        Console.WriteLine();
        totalRecords += schedules.Count;

        // Print sample of unique job codes found
        //var jobCodes = schedules
        //    .Select(s => $"{s.Route} - {s.BinType} - {s.DayOfWeek} {s.WeekCycle}")
        //    .Distinct()
        //    .OrderBy(j => j)
        //    .ToList();

        //Console.WriteLine($"  Job codes found: {string.Join(", ", jobCodes)}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  Error processing {fileName}: {ex.Message}");

        // Clear any tracked entities from the failed batch to prevent them
        // from being re-attempted when processing subsequent files
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}

Console.WriteLine($"\nSeeding complete. Total records inserted: {totalRecords:N0}");

// Print summary
var finalCount = await context.BinSchedules.CountAsync();
Console.WriteLine($"Database now contains {finalCount:N0} bin schedule records.");

return 0;

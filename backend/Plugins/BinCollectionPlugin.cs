using System.ComponentModel;
using Microsoft.SemanticKernel;
using BelfastBinsApi.Data;
using BelfastBinsApi.Models;
using BelfastBinsApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BelfastBinsApi.Plugins;

/// <summary>
/// Semantic Kernel plugin that exposes bin collection lookup functionality to the LLM.
/// </summary>
public class BinCollectionPlugin
{
    private readonly BinScraperService _scraperService;
    private readonly CollectionScheduleService _scheduleService;
    private readonly ILogger<BinCollectionPlugin> _logger;

    public BinCollectionPlugin(
        BinScraperService scraperService,
        CollectionScheduleService scheduleService,
        ILogger<BinCollectionPlugin> logger)
    {
        _scraperService = scraperService;
        _scheduleService = scheduleService;
        _logger = logger;
    }

    [KernelFunction("lookup_bin_collections")]
    [Description("Look up bin collection schedules for a Belfast address by postcode and optional house number. Returns all bin types (Black, Blue, Brown, Glass) with their next collection dates.")]
    public async Task<string> LookupBinCollectionsAsync(
        [Description("The Belfast postcode to look up, e.g. BT14 7GP")] string postcode,
        [Description("Optional house number to narrow down the address")] string? houseNumber = null)
    {
        _logger.LogInformation("SK Plugin: Looking up bins for {Postcode} {HouseNumber}", postcode, houseNumber);

        var result = await _scraperService.LookupFromDatabase(postcode, houseNumber);

        if (result == null)
        {
            return $"No bin collection data found for postcode {postcode}" +
                   (string.IsNullOrEmpty(houseNumber) ? "" : $" house number {houseNumber}") +
                   ". Please check the postcode is correct and is a Belfast postcode.";
        }

        var lines = new List<string>
        {
            $"Address: {result.Address}",
            $"Next collection: {result.NextCollectionColor} bin",
            "",
            "All upcoming collections:"
        };

        foreach (var collection in result.Collections)
        {
            lines.Add($"- {collection.BinType}: {collection.NextCollection}");
        }

        return string.Join("\n", lines);
    }

    [KernelFunction("get_next_collection_for_bin_type")]
    [Description("Get the next collection date for a specific bin type (Black, Blue, Brown, or Glass) at a Belfast address.")]
    public async Task<string> GetNextCollectionForBinTypeAsync(
        [Description("The Belfast postcode to look up")] string postcode,
        [Description("The bin type to check: Black, Blue, Brown, or Glass")] string binType,
        [Description("Optional house number to narrow down the address")] string? houseNumber = null)
    {
        _logger.LogInformation("SK Plugin: Looking up {BinType} bin for {Postcode}", binType, postcode);

        var result = await _scraperService.LookupFromDatabase(postcode, houseNumber);

        if (result == null)
        {
            return $"No bin collection data found for postcode {postcode}.";
        }

        var collection = result.Collections
            .FirstOrDefault(c => c.Color.Equals(binType, StringComparison.OrdinalIgnoreCase));

        if (collection == null)
        {
            return $"No {binType} bin collection found for this address. Available bins: " +
                   string.Join(", ", result.Collections.Select(c => c.Color));
        }

        return $"The next {collection.BinType} collection at {result.Address} is on {collection.NextCollection}.";
    }

    [KernelFunction("get_recycling_guidance")]
    [Description("Get Belfast-specific recycling guidance about what goes in each bin type. Use this when users ask what they can put in a particular bin or how to recycle something.")]
    public string GetRecyclingGuidance(
        [Description("The bin type to get guidance for: Black, Blue, Brown, or Glass")] string binType)
    {
        return binType.ToUpperInvariant() switch
        {
            "BLACK" => "Black Bin (General Waste): Non-recyclable household waste including nappies, polystyrene, crisp packets, cling film, broken crockery, and cat litter. Do NOT put recyclable items in the black bin.",

            "BLUE" => "Blue Bin (Recycling): Clean and dry recyclables including paper, cardboard, plastic bottles and containers, food tins, drink cans, aerosol cans, and Tetra Pak cartons. NO food waste, nappies, or textiles.",

            "BROWN" => "Brown Bin (Garden Waste): Garden waste only including grass cuttings, hedge trimmings, leaves, weeds, small branches, and plants. NO food waste, soil, or non-garden waste.",

            "GLASS" => "Glass Box (Glass Recycling): Glass bottles and glass jars only. Remove lids and rinse. NO broken glass, mirrors, window glass, Pyrex, or light bulbs.",

            _ => $"Unknown bin type '{binType}'. Belfast has four bin types: Black (general waste), Blue (recycling), Brown (garden waste), and Glass (glass recycling)."
        };
    }

    [KernelFunction("get_today_info")]
    [Description("Get the current date and day of the week. Useful for answering questions about 'this week' or 'today'.")]
    public string GetTodayInfo()
    {
        var now = DateTime.Now;
        return $"Today is {now:dddd, dd MMMM yyyy}. The current week type is {_scheduleService.GetWeekType(now)}.";
    }
}

namespace BelfastBinsApi.Models;

public class BinSchedule
{
    public int Id { get; set; }
    public required string Route { get; set; }
    public required string BinType { get; set; }
    public required string DayOfWeek { get; set; }
    public required string WeekCycle { get; set; }
    public required string HouseNumber { get; set; }
    public string HouseSuffix { get; set; } = string.Empty;
    public required string Street { get; set; }
    public required string StreetNormalized { get; set; }
    public required string City { get; set; }
    public required string County { get; set; }
    public required string Postcode { get; set; }
    public required string PostcodeNormalized { get; set; }
    public required string FullAddress { get; set; }
}

namespace BelfastBinsApi.Models;

public class BinLookupRequest
{
    public string Postcode { get; set; } = string.Empty;
    public string? HouseNumber { get; set; }
}

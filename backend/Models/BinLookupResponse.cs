namespace BelfastBinsApi.Models;

public class BinLookupResponse
{
    public string Address { get; set; } = string.Empty;
    public List<BinCollection> Collections { get; set; } = new();
    public string NextCollectionColor { get; set; } = string.Empty;
}

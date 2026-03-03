namespace BelfastBinsApi.Models;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Postcode { get; set; }
    public string? HouseNumber { get; set; }
    public string? SessionId { get; set; }
}

namespace BelfastBinsApi.Models;

public class SmsSubscription
{
    public int Id { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Postcode { get; set; }
    public string? HouseNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastNotifiedAt { get; set; }
}

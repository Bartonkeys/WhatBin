namespace BelfastBinsApi.Models;

public class NotificationLog
{
    public int Id { get; set; }
    public int SmsSubscriptionId { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Message { get; set; }
    public required string BinType { get; set; }
    public DateTime CollectionDate { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public SmsSubscription? Subscription { get; set; }
}

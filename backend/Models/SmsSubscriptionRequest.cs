namespace BelfastBinsApi.Models;

public class SmsSubscriptionRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string? HouseNumber { get; set; }
}

public class SmsUnsubscribeRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}

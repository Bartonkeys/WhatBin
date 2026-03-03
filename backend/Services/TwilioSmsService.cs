using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace BelfastBinsApi.Services;

public interface ISmsService
{
    Task<bool> SendSmsAsync(string toPhoneNumber, string message);
    bool IsConfigured { get; }
}

public class TwilioSmsService : ISmsService
{
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _fromNumber;

    public bool IsConfigured => !string.IsNullOrEmpty(_accountSid)
                             && !string.IsNullOrEmpty(_authToken)
                             && !string.IsNullOrEmpty(_fromNumber);

    public TwilioSmsService(IConfiguration configuration, ILogger<TwilioSmsService> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("Twilio");
        _accountSid = section["AccountSid"];
        _authToken = section["AuthToken"];
        _fromNumber = section["FromNumber"];

        if (IsConfigured)
        {
            TwilioClient.Init(_accountSid, _authToken);
            _logger.LogInformation("Twilio SMS service initialized");
        }
        else
        {
            _logger.LogWarning("Twilio SMS service not configured. SMS notifications will be disabled. Set Twilio:AccountSid, Twilio:AuthToken, and Twilio:FromNumber in configuration.");
        }
    }

    public async Task<bool> SendSmsAsync(string toPhoneNumber, string message)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Attempted to send SMS but Twilio is not configured. Message to {To}: {Message}", toPhoneNumber, message);
            return false;
        }

        try
        {
            var messageResource = await MessageResource.CreateAsync(
                to: new PhoneNumber(toPhoneNumber),
                from: new PhoneNumber(_fromNumber),
                body: message);

            _logger.LogInformation("SMS sent to {To}, SID: {Sid}, Status: {Status}",
                toPhoneNumber, messageResource.Sid, messageResource.Status);

            return messageResource.Status != MessageResource.StatusEnum.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {To}", toPhoneNumber);
            return false;
        }
    }
}

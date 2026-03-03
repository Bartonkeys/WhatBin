using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelfastBinsApi.Data;
using BelfastBinsApi.Models;
using BelfastBinsApi.Services;

namespace BelfastBinsApi.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly BinDbContext _dbContext;
    private readonly ISmsService _smsService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        BinDbContext dbContext,
        ISmsService smsService,
        ILogger<NotificationController> logger)
    {
        _dbContext = dbContext;
        _smsService = smsService;
        _logger = logger;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SmsSubscriptionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Postcode))
            {
                return BadRequest(new { detail = "Phone number and postcode are required" });
            }

            // Normalize phone number (ensure it has country code)
            var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
            if (phoneNumber == null)
            {
                return BadRequest(new { detail = "Invalid phone number format. Please use international format (e.g., +44 7700 900000)" });
            }

            // Check for existing subscription
            var existing = await _dbContext.SmsSubscriptions
                .FirstOrDefaultAsync(s => s.PhoneNumber == phoneNumber && s.Postcode == request.Postcode.Trim().ToUpper());

            if (existing != null)
            {
                if (existing.IsActive)
                {
                    return Ok(new { message = "You are already subscribed for this address", subscriptionId = existing.Id });
                }

                // Reactivate
                existing.IsActive = true;
                existing.HouseNumber = request.HouseNumber;
                await _dbContext.SaveChangesAsync();
                return Ok(new { message = "Subscription reactivated", subscriptionId = existing.Id });
            }

            var subscription = new SmsSubscription
            {
                PhoneNumber = phoneNumber,
                Postcode = request.Postcode.Trim().ToUpper(),
                HouseNumber = request.HouseNumber?.Trim()
            };

            _dbContext.SmsSubscriptions.Add(subscription);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("New SMS subscription: {Phone} for {Postcode}", phoneNumber, request.Postcode);

            // Send confirmation SMS if Twilio is configured
            if (_smsService.IsConfigured)
            {
                await _smsService.SendSmsAsync(phoneNumber,
                    $"Welcome to WhatBin! You'll receive bin collection reminders the evening before your collection day for {request.Postcode.Trim().ToUpper()}. Reply STOP to unsubscribe.");
            }

            return Ok(new
            {
                message = "Successfully subscribed to bin collection reminders",
                subscriptionId = subscription.Id,
                smsEnabled = _smsService.IsConfigured
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing");
            return StatusCode(500, new { detail = "Error creating subscription" });
        }
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] SmsUnsubscribeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { detail = "Phone number is required" });
            }

            var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
            if (phoneNumber == null)
            {
                return BadRequest(new { detail = "Invalid phone number format" });
            }

            var subscriptions = await _dbContext.SmsSubscriptions
                .Where(s => s.PhoneNumber == phoneNumber && s.IsActive)
                .ToListAsync();

            if (!subscriptions.Any())
            {
                return NotFound(new { detail = "No active subscriptions found for this phone number" });
            }

            foreach (var sub in subscriptions)
            {
                sub.IsActive = false;
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Unsubscribed {Phone} from {Count} subscriptions", phoneNumber, subscriptions.Count);

            return Ok(new { message = $"Unsubscribed from {subscriptions.Count} notification(s)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing");
            return StatusCode(500, new { detail = "Error processing unsubscribe request" });
        }
    }

    [HttpGet("status/{phoneNumber}")]
    public async Task<IActionResult> GetStatus(string phoneNumber)
    {
        try
        {
            var normalized = NormalizePhoneNumber(phoneNumber);
            if (normalized == null)
            {
                return BadRequest(new { detail = "Invalid phone number format" });
            }

            var subscriptions = await _dbContext.SmsSubscriptions
                .Where(s => s.PhoneNumber == normalized)
                .Select(s => new
                {
                    s.Id,
                    s.Postcode,
                    s.HouseNumber,
                    s.IsActive,
                    s.CreatedAt,
                    s.LastNotifiedAt
                })
                .ToListAsync();

            return Ok(new
            {
                phoneNumber = normalized,
                subscriptions,
                smsEnabled = _smsService.IsConfigured
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking status");
            return StatusCode(500, new { detail = "Error checking subscription status" });
        }
    }

    private static string? NormalizePhoneNumber(string phone)
    {
        // Strip spaces and dashes
        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        // Handle UK numbers
        if (cleaned.StartsWith("07") && cleaned.Length == 11)
        {
            return "+44" + cleaned[1..];
        }

        if (cleaned.StartsWith("+44") && cleaned.Length == 13)
        {
            return cleaned;
        }

        if (cleaned.StartsWith("0044") && cleaned.Length == 14)
        {
            return "+" + cleaned[2..];
        }

        // Accept any number starting with +
        if (cleaned.StartsWith("+") && cleaned.Length >= 10)
        {
            return cleaned;
        }

        return null;
    }
}

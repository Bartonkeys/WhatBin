using BelfastBinsApi.Data;
using BelfastBinsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BelfastBinsApi.Services;

/// <summary>
/// Background service that runs daily to send SMS reminders the night before bin collections.
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;

    // Run at 6 PM daily (night before collection)
    private static readonly TimeSpan RunTime = new(18, 0, 0);

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background service started. Will run daily at {RunTime}", RunTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.Add(RunTime);

            // If we've already passed today's run time, schedule for tomorrow
            if (now > nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            _logger.LogInformation("Next notification run scheduled at {NextRun} (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SendNotificationsAsync(stoppingToken);
        }

        _logger.LogInformation("Notification background service stopped");
    }

    public async Task SendNotificationsAsync(CancellationToken stoppingToken = default)
    {
        _logger.LogInformation("Running notification check at {Time}", DateTime.Now);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BinDbContext>();
        var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();
        var scheduleService = scope.ServiceProvider.GetRequiredService<CollectionScheduleService>();

        if (!smsService.IsConfigured)
        {
            _logger.LogWarning("SMS service not configured, skipping notifications");
            return;
        }

        var activeSubscriptions = await dbContext.SmsSubscriptions
            .Where(s => s.IsActive)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Found {Count} active subscriptions", activeSubscriptions.Count);

        var tomorrow = DateTime.Now.Date.AddDays(1);

        foreach (var subscription in activeSubscriptions)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await ProcessSubscriptionAsync(
                    subscription, tomorrow, dbContext, smsService, scheduleService, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription {Id} for {Phone}",
                    subscription.Id, subscription.PhoneNumber);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private async Task ProcessSubscriptionAsync(
        SmsSubscription subscription,
        DateTime tomorrow,
        BinDbContext dbContext,
        ISmsService smsService,
        CollectionScheduleService scheduleService,
        CancellationToken stoppingToken)
    {
        var postcodeNormalized = subscription.Postcode.Replace(" ", "").ToUpper();

        var query = dbContext.BinSchedules
            .Where(s => s.PostcodeNormalized == postcodeNormalized);

        if (!string.IsNullOrEmpty(subscription.HouseNumber))
        {
            var houseNumberNormalized = subscription.HouseNumber.Trim().ToUpper();
            query = query.Where(s => s.HouseNumber == houseNumberNormalized ||
                                     (s.HouseNumber + s.HouseSuffix) == houseNumberNormalized);
        }

        var schedules = await query.ToListAsync(stoppingToken);
        if (!schedules.Any()) return;

        var address = schedules.First().FullAddress;

        // Check each bin type for collections tomorrow
        var binsDueTomorrow = new List<string>();

        var byBinType = schedules
            .Where(s => !string.IsNullOrEmpty(s.BinType) && !string.IsNullOrEmpty(s.WeekCycle))
            .GroupBy(s => s.BinType, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byBinType)
        {
            var schedule = group.First();
            var nextDate = scheduleService.GetNextCollectionDate(schedule.BinType, schedule.WeekCycle, schedule.DayOfWeek);

            if (nextDate?.Date == tomorrow.Date)
            {
                binsDueTomorrow.Add(CollectionScheduleService.GetBinDisplayName(schedule.BinType));
            }
        }

        if (!binsDueTomorrow.Any()) return;

        // Check if we already sent a notification for this date
        var alreadySent = await dbContext.NotificationLogs
            .AnyAsync(n => n.SmsSubscriptionId == subscription.Id
                        && n.CollectionDate.Date == tomorrow.Date
                        && n.Success,
                stoppingToken);

        if (alreadySent)
        {
            _logger.LogInformation("Already sent notification to {Phone} for {Date}, skipping",
                subscription.PhoneNumber, tomorrow.Date);
            return;
        }

        // Build and send the message
        var binList = string.Join(" and ", binsDueTomorrow);
        var message = $"WhatBin Reminder: Your {binList} collection is tomorrow ({tomorrow:dddd, dd MMMM}). Don't forget to put your bins out! Reply STOP to unsubscribe.";

        var success = await smsService.SendSmsAsync(subscription.PhoneNumber, message);

        // Log the notification
        dbContext.NotificationLogs.Add(new NotificationLog
        {
            SmsSubscriptionId = subscription.Id,
            PhoneNumber = subscription.PhoneNumber,
            Message = message,
            BinType = string.Join(", ", binsDueTomorrow),
            CollectionDate = tomorrow,
            Success = success,
            ErrorMessage = success ? null : "SMS send failed"
        });

        if (success)
        {
            subscription.LastNotifiedAt = DateTime.UtcNow;
            _logger.LogInformation("Sent reminder to {Phone} for {Bins} on {Date}",
                subscription.PhoneNumber, binList, tomorrow.Date);
        }
    }
}

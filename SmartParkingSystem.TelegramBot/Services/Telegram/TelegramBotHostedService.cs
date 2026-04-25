using Microsoft.Extensions.Options;
using SmartParkingSystem.TelegramBot.Configuration;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramBotHostedService(
    TelegramBotApiClient apiClient,
    IOptions<TelegramBotOptions> options,
    TelegramBotUpdateHandler updateHandler) : BackgroundService
{
    private long? _nextOffset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            return;
        }

        await apiClient.GetMeAsync(stoppingToken);

        if (!settings.UseWebhook)
        {
            await apiClient.DeleteWebhookAsync(stoppingToken);
            await RunPollingLoopAsync(settings, stoppingToken);
        }
    }

    private async Task RunPollingLoopAsync(
        TelegramBotOptions settings,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updatesResponse = await apiClient.GetUpdatesAsync(
                    _nextOffset,
                    settings.PollingTimeoutSeconds,
                    stoppingToken);

                var updates = updatesResponse?.Result ?? [];
                foreach (var update in updates)
                {
                    _nextOffset = update.UpdateId + 1;
                    await updateHandler.HandleAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
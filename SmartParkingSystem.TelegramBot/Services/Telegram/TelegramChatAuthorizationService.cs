using Microsoft.Extensions.Options;
using SmartParkingSystem.TelegramBot.Configuration;

namespace SmartParkingSystem.TelegramBot.Services.Telegram;

public sealed class TelegramChatAuthorizationService(IOptions<TelegramBotOptions> options)
{
    private readonly HashSet<long> _allowedChatIds = options.Value.AllowedChatIds
        .Select(static value => long.TryParse(value, out var parsed) ? parsed : (long?)null)
        .OfType<long>()
        .ToHashSet();

    public bool IsAllowed(long chatId)
    {
        return _allowedChatIds.Count == 0 || _allowedChatIds.Contains(chatId);
    }
}
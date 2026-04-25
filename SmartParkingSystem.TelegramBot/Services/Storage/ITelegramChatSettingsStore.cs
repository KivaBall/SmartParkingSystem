using SmartParkingSystem.TelegramBot.Models.Telegram;

namespace SmartParkingSystem.TelegramBot.Services.Storage;

public interface ITelegramChatSettingsStore
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<TelegramChatSettings?> GetChatSettingsAsync(long chatId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TelegramChatSettings>> GetAllChatSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveChatSettingsAsync(TelegramChatSettings settings, CancellationToken cancellationToken = default);
}
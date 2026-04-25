namespace SmartParkingSystem.TelegramBot.Configuration;

public sealed class SqliteStorageOptions
{
    public const string SectionName = "SqliteStorage";

    public string DatabasePath { get; set; } = "data/telegram-bot.db";
}
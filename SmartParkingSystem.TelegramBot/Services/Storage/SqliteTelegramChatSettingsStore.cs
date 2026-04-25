using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SmartParkingSystem.TelegramBot.Configuration;
using SmartParkingSystem.TelegramBot.Models.Telegram;

namespace SmartParkingSystem.TelegramBot.Services.Storage;

public sealed class SqliteTelegramChatSettingsStore(
    IOptions<SqliteStorageOptions> options,
    IHostEnvironment hostEnvironment) : ITelegramChatSettingsStore
{
    private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);
    private bool _isInitialized;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            var databasePath = ResolveDatabasePath();
            var databaseDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                                  CREATE TABLE IF NOT EXISTS TelegramChatSettings (
                                      ChatId INTEGER PRIMARY KEY,
                                      Language TEXT NULL,
                                      BotEnabled INTEGER NOT NULL DEFAULT 0,
                                      NotificationsEnabled INTEGER NOT NULL,
                                      GateNotificationsEnabled INTEGER NOT NULL,
                                      ParkingNotificationsEnabled INTEGER NOT NULL,
                                      MonitorNotificationsEnabled INTEGER NOT NULL,
                                      AdminNotificationsEnabled INTEGER NOT NULL,
                                      ConnectionNotificationsEnabled INTEGER NOT NULL,
                                      LastDeliveredEventId TEXT NULL
                                  );
                                  """;

            await command.ExecuteNonQueryAsync(cancellationToken);
            await EnsureBotEnabledColumnAsync(connection, cancellationToken);
            await EnsureLastDeliveredEventIdColumnAsync(connection, cancellationToken);

            _isInitialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task<TelegramChatSettings?> GetChatSettingsAsync(
        long chatId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var databasePath = ResolveDatabasePath();

        await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  ChatId,
                                  Language,
                                  BotEnabled,
                                  NotificationsEnabled,
                                  GateNotificationsEnabled,
                                  ParkingNotificationsEnabled,
                                  MonitorNotificationsEnabled,
                                  AdminNotificationsEnabled,
                                  ConnectionNotificationsEnabled,
                                  LastDeliveredEventId
                              FROM TelegramChatSettings
                              WHERE ChatId = $chatId
                              LIMIT 1;
                              """;

        command.Parameters.AddWithValue("$chatId", chatId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TelegramChatSettings(
            reader.GetInt64(0),
            ParseLanguage(reader.IsDBNull(1) ? null : reader.GetString(1)),
            reader.GetInt64(2) == 1,
            reader.GetInt64(3) == 1,
            reader.GetInt64(4) == 1,
            reader.GetInt64(5) == 1,
            reader.GetInt64(6) == 1,
            reader.GetInt64(7) == 1,
            reader.GetInt64(8) == 1,
            reader.IsDBNull(9) ? null : reader.GetString(9));
    }

    public async Task<IReadOnlyList<TelegramChatSettings>> GetAllChatSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var databasePath = ResolveDatabasePath();
        var result = new List<TelegramChatSettings>();

        await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  ChatId,
                                  Language,
                                  BotEnabled,
                                  NotificationsEnabled,
                                  GateNotificationsEnabled,
                                  ParkingNotificationsEnabled,
                                  MonitorNotificationsEnabled,
                                  AdminNotificationsEnabled,
                                  ConnectionNotificationsEnabled,
                                  LastDeliveredEventId
                              FROM TelegramChatSettings;
                              """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new TelegramChatSettings(
                    reader.GetInt64(0),
                    ParseLanguage(reader.IsDBNull(1) ? null : reader.GetString(1)),
                    reader.GetInt64(2) == 1,
                    reader.GetInt64(3) == 1,
                    reader.GetInt64(4) == 1,
                    reader.GetInt64(5) == 1,
                    reader.GetInt64(6) == 1,
                    reader.GetInt64(7) == 1,
                    reader.GetInt64(8) == 1,
                    reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return result;
    }

    public async Task SaveChatSettingsAsync(
        TelegramChatSettings settings,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var databasePath = ResolveDatabasePath();

        await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO TelegramChatSettings (
                                  ChatId,
                                  Language,
                                  BotEnabled,
                                  NotificationsEnabled,
                                  GateNotificationsEnabled,
                                  ParkingNotificationsEnabled,
                                  MonitorNotificationsEnabled,
                                  AdminNotificationsEnabled,
                                  ConnectionNotificationsEnabled,
                                  LastDeliveredEventId
                              )
                              VALUES (
                                  $chatId,
                                  $language,
                                  $botEnabled,
                                  $notificationsEnabled,
                                  $gateNotificationsEnabled,
                                  $parkingNotificationsEnabled,
                                  $monitorNotificationsEnabled,
                                  $adminNotificationsEnabled,
                                  $connectionNotificationsEnabled,
                                  $lastDeliveredEventId
                              )
                              ON CONFLICT(ChatId) DO UPDATE SET
                                  Language = excluded.Language,
                                  BotEnabled = excluded.BotEnabled,
                                  NotificationsEnabled = excluded.NotificationsEnabled,
                                  GateNotificationsEnabled = excluded.GateNotificationsEnabled,
                                  ParkingNotificationsEnabled = excluded.ParkingNotificationsEnabled,
                                  MonitorNotificationsEnabled = excluded.MonitorNotificationsEnabled,
                                  AdminNotificationsEnabled = excluded.AdminNotificationsEnabled,
                                  ConnectionNotificationsEnabled = excluded.ConnectionNotificationsEnabled,
                                  LastDeliveredEventId = excluded.LastDeliveredEventId;
                              """;

        command.Parameters.AddWithValue("$chatId", settings.ChatId);
        command.Parameters.AddWithValue(
            "$language",
            settings.Language is null ? DBNull.Value : ToStorageLanguage(settings.Language.Value));
        command.Parameters.AddWithValue("$botEnabled", settings.BotEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$notificationsEnabled", settings.NotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$gateNotificationsEnabled", settings.GateNotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$parkingNotificationsEnabled", settings.ParkingNotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$monitorNotificationsEnabled", settings.MonitorNotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$adminNotificationsEnabled", settings.AdminNotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue(
            "$connectionNotificationsEnabled",
            settings.ConnectionNotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue(
            "$lastDeliveredEventId",
            settings.LastDeliveredEventId is null ? DBNull.Value : settings.LastDeliveredEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureBotEnabledColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (await HasColumnAsync(connection, "BotEnabled", cancellationToken))
        {
            return;
        }

        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE TelegramChatSettings ADD COLUMN BotEnabled INTEGER NOT NULL DEFAULT 0;";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureLastDeliveredEventIdColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (await HasColumnAsync(connection, "LastDeliveredEventId", cancellationToken))
        {
            return;
        }

        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE TelegramChatSettings ADD COLUMN LastDeliveredEventId TEXT NULL;";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> HasColumnAsync(
        SqliteConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('TelegramChatSettings');";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string ResolveDatabasePath()
    {
        var configuredPath = options.Value.DatabasePath.Trim();
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath);
    }

    private static string BuildConnectionString(string databasePath)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        };

        return connectionStringBuilder.ToString();
    }

    private static TelegramChatLanguage? ParseLanguage(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "uk" => TelegramChatLanguage.Ukrainian,
            "en" => TelegramChatLanguage.English,
            _ => null
        };
    }

    private static string ToStorageLanguage(TelegramChatLanguage language)
    {
        return language switch
        {
            TelegramChatLanguage.Ukrainian => "uk",
            TelegramChatLanguage.English => "en",
            _ => "uk"
        };
    }
}

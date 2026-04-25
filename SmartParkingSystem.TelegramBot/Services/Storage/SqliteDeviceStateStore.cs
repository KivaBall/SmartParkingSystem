using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SmartParkingSystem.Domain.Models.BackendSync;
using SmartParkingSystem.TelegramBot.Configuration;

namespace SmartParkingSystem.TelegramBot.Services.Storage;

public sealed class SqliteDeviceStateStore(
    IOptions<SqliteStorageOptions> options,
    IHostEnvironment hostEnvironment) : IDeviceStateStore
{
    private static readonly JsonSerializerOptions PayloadJsonOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

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
                                  CREATE TABLE IF NOT EXISTS DeviceStateSnapshots (
                                      Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      CapturedAtUtc TEXT NOT NULL,
                                      ReceivedAtUtc TEXT NOT NULL,
                                      SourcePlatform TEXT NOT NULL,
                                      AppVersion TEXT NOT NULL,
                                      IsConnected INTEGER NOT NULL,
                                      EventCount INTEGER NOT NULL,
                                      PayloadJson TEXT NOT NULL
                                  );

                                  CREATE INDEX IF NOT EXISTS IX_DeviceStateSnapshots_CapturedAtUtc
                                      ON DeviceStateSnapshots(CapturedAtUtc DESC);
                                  """;

            await command.ExecuteNonQueryAsync(cancellationToken);

            _isInitialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task SaveSnapshotAsync(
        BackendDeviceStatePayload payload,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var databasePath = ResolveDatabasePath();
        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);

        await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO DeviceStateSnapshots (
                                  CapturedAtUtc,
                                  ReceivedAtUtc,
                                  SourcePlatform,
                                  AppVersion,
                                  IsConnected,
                                  EventCount,
                                  PayloadJson
                              )
                              VALUES (
                                  $capturedAtUtc,
                                  $receivedAtUtc,
                                  $sourcePlatform,
                                  $appVersion,
                                  $isConnected,
                                  $eventCount,
                                  $payloadJson
                              );
                              """;

        command.Parameters.AddWithValue("$capturedAtUtc", payload.CapturedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$receivedAtUtc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$sourcePlatform", payload.SourcePlatform);
        command.Parameters.AddWithValue("$appVersion", payload.AppVersion);
        command.Parameters.AddWithValue("$isConnected", payload.Dashboard.IsConnected ? 1 : 0);
        command.Parameters.AddWithValue("$eventCount", payload.RecentEvents.Count);
        command.Parameters.AddWithValue("$payloadJson", payloadJson);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BackendDeviceStatePayload?> GetLatestSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var databasePath = ResolveDatabasePath();

        await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT PayloadJson
                              FROM DeviceStateSnapshots
                              ORDER BY Id DESC
                              LIMIT 1;
                              """;

        var payloadJson = await command.ExecuteScalarAsync(cancellationToken) as string;
        return string.IsNullOrWhiteSpace(payloadJson)
            ? null
            : JsonSerializer.Deserialize<BackendDeviceStatePayload>(payloadJson, PayloadJsonOptions);
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
}
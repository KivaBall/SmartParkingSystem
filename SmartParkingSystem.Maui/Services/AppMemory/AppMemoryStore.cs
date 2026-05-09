using System.Text.Json;
using System.Text.Json.Serialization;
using SmartParkingSystem.Domain.Models.Events;
using SmartParkingSystem.Domain.Models.Localization;
using SmartParkingSystem.Domain.Models.Parking;

namespace SmartParkingSystem.Maui.Services.AppMemory;

public sealed class AppMemoryStore : IAppMemoryStore
{
    private const string EventsFileName = "events.json";
    private const string LanguageFileName = "language.json";
    private const string SmartParkingProfilesFileName = "smart-parking-profiles.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _directoryPath;
    private readonly Lock _sync = new Lock();

    public AppMemoryStore()
    {
        _directoryPath = Path.Combine(FileSystem.AppDataDirectory, "app-memory");
    }

    public AppLanguage GetLanguage(AppLanguage defaultLanguage)
    {
        lock (_sync)
        {
            var storedLanguage = ReadFile<StoredLanguage>(LanguageFileName);
            if (storedLanguage is not null && Enum.IsDefined(storedLanguage.Language))
            {
                return storedLanguage.Language;
            }

            return defaultLanguage;
        }
    }

    public void SetLanguage(AppLanguage language)
    {
        lock (_sync)
        {
            WriteFile(LanguageFileName, new StoredLanguage(language));
        }
    }

    public IReadOnlyList<EventFeedItem> GetEvents()
    {
        lock (_sync)
        {
            return ReadFile<EventFeedItem[]>(EventsFileName) ?? [];
        }
    }

    public void SetEvents(IReadOnlyList<EventFeedItem> events)
    {
        lock (_sync)
        {
            WriteFile(EventsFileName, events);
        }
    }

    public IReadOnlyList<SmartParkingCardProfile> GetSmartParkingCardProfiles()
    {
        lock (_sync)
        {
            return ReadFile<SmartParkingCardProfile[]>(SmartParkingProfilesFileName) ?? [];
        }
    }

    public void SetSmartParkingCardProfiles(IReadOnlyList<SmartParkingCardProfile> profiles)
    {
        lock (_sync)
        {
            WriteFile(SmartParkingProfilesFileName, profiles);
        }
    }

    private T? ReadFile<T>(string fileName)
    {
        var path = GetFilePath(fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private void WriteFile<T>(string fileName, T value)
    {
        Directory.CreateDirectory(_directoryPath);

        var path = GetFilePath(fileName);
        var temporaryPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(value, JsonOptions);

        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, true);
    }

    private string GetFilePath(string fileName)
    {
        return Path.Combine(_directoryPath, fileName);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record StoredLanguage(AppLanguage Language);
}
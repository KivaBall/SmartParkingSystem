using System.Text.Json.Serialization;
using SmartParkingSystem.TelegramBot.Configuration;
using SmartParkingSystem.TelegramBot.Hubs;
using SmartParkingSystem.TelegramBot.Services.Commands;
using SmartParkingSystem.TelegramBot.Services.DeviceState;
using SmartParkingSystem.TelegramBot.Services.Storage;
using SmartParkingSystem.TelegramBot.Services.Telegram;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<TelegramBotOptions>()
    .Bind(builder.Configuration.GetSection(TelegramBotOptions.SectionName));
builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.AddOptions<SqliteStorageOptions>()
    .Bind(builder.Configuration.GetSection(SqliteStorageOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(cors =>
{
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

    cors.AddPolicy(
        "FrontendClients",
        policy =>
        {
            if (corsOptions.AllowedOrigins.Length == 0)
            {
                policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true);
                return;
            }

            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});
builder.Services.AddSignalR()
    .AddJsonProtocol(options => { options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });
builder.Services.AddHttpClient(TelegramBotApiClient.HttpClientName);
builder.Services.AddSingleton<ConnectedDeviceHostRegistry>();
builder.Services.AddSingleton<BackendCommandDispatchService>();
builder.Services.AddSingleton<TelegramBotApiClient>();
builder.Services.AddSingleton<DeviceStateCache>();
builder.Services.AddSingleton<DeviceStateIngestService>();
builder.Services.AddSingleton<IDeviceStateStore, SqliteDeviceStateStore>();
builder.Services.AddSingleton<ITelegramChatSettingsStore, SqliteTelegramChatSettingsStore>();
builder.Services.AddSingleton<TelegramChatAuthorizationService>();
builder.Services.AddSingleton<TelegramChatSettingsService>();
builder.Services.AddSingleton<TelegramMenuService>();
builder.Services.AddSingleton<TelegramNotificationService>();
builder.Services.AddSingleton<TelegramBotUpdateHandler>();
builder.Services.AddHostedService<DeviceStateStorageHostedService>();
builder.Services.AddHostedService<TelegramBotHostedService>();

var app = builder.Build();

app.UseCors("FrontendClients");

app.MapGet(
    "/",
    (IHostEnvironment environment) => Results.Ok(
        new
        {
            service = "SmartParkingSystem.TelegramBot",
            environment = environment.EnvironmentName,
            status = "ready"
        }));

app.MapGet(
    "/health",
    () => Results.Ok(
        new
        {
            status = "ok",
            service = "SmartParkingSystem.TelegramBot",
            utcNow = DateTimeOffset.UtcNow
        }));

app.MapHub<DeviceStateHub>(DeviceStateHub.Path)
    .RequireCors("FrontendClients");

app.Run();
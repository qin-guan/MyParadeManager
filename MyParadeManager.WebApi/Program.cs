using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Options;
using MyParadeManager.WebApi.BackgroundServices;
using MyParadeManager.WebApi.GoogleSheets;
using MyParadeManager.WebApi.GoogleSheets.ValueConverter;
using MyParadeManager.WebApi.Options;
using ZiggyCreatures.Caching.Fusion;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection("Telegram"));

builder.Services.AddOptions<GoogleOptions>()
    .Bind(builder.Configuration.GetSection("Google"));

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<GoogleOptions>>();

    var credential = GoogleCredential.FromJson(options.Value.ServiceAccountCredentials);
    var service = new SheetsService(new BaseClientService.Initializer
    {
        HttpClientInitializer = credential
    });

    return service;
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<GoogleOptions>>();
    return new GoogleSheetsConfiguration(options.Value.SpreadsheetId);
});

builder.Services.AddScoped<IGoogleSheetsContext, GoogleSheetsContext>();
builder.Services.AddHostedService<Bot>();

builder.Services.AddFusionCache().AsHybridCache();

var app = builder.Build();

app.Run();
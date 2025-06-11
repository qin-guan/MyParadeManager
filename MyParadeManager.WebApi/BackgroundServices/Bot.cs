using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using MyParadeManager.WebApi.Forms;
using MyParadeManager.WebApi.Options;
using TelegramBotBase;
using TelegramBotBase.Builder;
using TelegramBotBase.Commands;

namespace MyParadeManager.WebApi.BackgroundServices;

public class Bot : BackgroundService
{
    private readonly ILogger<Bot> _logger;
    private readonly BotBase _bot;

    public Bot(IOptions<TelegramOptions> telegramOptions, ILogger<Bot> logger, HybridCache cache, IServiceProvider sp)
    {
        _logger = logger;
        ThreadPool.GetMaxThreads(out var maxW, out var maxIo);
        _bot = BotBaseBuilder
            .Create()
            .WithAPIKey(telegramOptions.Value.Token)
            .DefaultMessageLoop()
            .WithServiceProvider<StartForm>(sp)
            .NoProxy()
            .CustomCommands(a =>
            {
                a.Start("Starts the bot");
                a.Add("purge", "Purge cache");
            })
            .NoSerialization()
            .UseEnglish()
            .UseThreadPool(maxW, maxIo)
            .Build();

        _bot.Exception += (_, ex) =>
        {
            _logger.LogError(ex.Error, "Exception occurred in bot handler");
        };

        _bot.BotCommand += async (sender, en) =>
        {
            if (en.Command != "/purge") return;
            await cache.RemoveByTagAsync("Purgeable");
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Uploading bot commands");
        await _bot.UploadBotCommands();
        _logger.LogInformation("Starting bot");
        await _bot.Start();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping bot");
        await _bot.Stop();
        _logger.LogInformation("Stopped bot");
    }
}
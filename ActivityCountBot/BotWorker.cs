using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ActivityCountBot;

public class BotWorker : BackgroundService
{
    private readonly TelegramBotClient _bot;
    private readonly BotHandler _handler;
    private readonly ILogger<BotWorker> _logger;

    public BotWorker(
        TelegramBotClient bot,
        BotHandler handler,
        ILogger<BotWorker> logger)
    {
        _bot = bot;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot worker starting...");

        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("Bot authenticated as @{BotUsername}", me.Username);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.EditedMessage
            },
            DropPendingUpdates = true
        };

        await _bot.ReceiveAsync(
            updateHandler: new UpdateHandler(_handler, _logger),
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);
    }
}

public class UpdateHandler : IUpdateHandler
{
    private readonly BotHandler _handler;
    private readonly ILogger _logger;

    public UpdateHandler(BotHandler handler, ILogger logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        await _handler.HandleUpdateAsync(update, cancellationToken);
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Polling error (Source: {ErrorSource})", errorSource.ToString());
        
        if (exception is Telegram.Bot.Exceptions.ApiRequestException apiException 
            && apiException.ErrorCode == 429)
        {
            _logger.LogWarning("Rate limit hit, waiting...");
        }
        
        return Task.CompletedTask;
    }
}
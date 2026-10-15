using ActivityCountBot.Database;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ActivityCountBot;

public class BotHandler
{
    private readonly TelegramBotClient _bot;
    private readonly MessageRepository _repository;
    private readonly ILogger<BotHandler> _logger;

    public BotHandler(
        TelegramBotClient bot,
        MessageRepository repository,
        ILogger<BotHandler> logger)
    {
        _bot = bot;
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message is { } message)
            {
                await HandleMessageAsync(message, cancellationToken);
            }
            else if (update.EditedMessage is { } editedMessage)
            {
                await HandleMessageAsync(editedMessage, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
            return;

        if (message.From is not { } user)
            return;
        
        if (message.Text?.StartsWith("/stats") == true)
        {
            await HandleStatsCommandAsync(message, user, cancellationToken);
            return;
        }

        await _repository.EnqueueAsync(new MessageEntry
        {
            ChatId = message.Chat.Id,
            ThreadId = message.MessageThreadId,
            UserId = user.Id,
            Username = user.Username,
            SentAt = message.Date.ToUniversalTime()
        }, cancellationToken);
    }

    private async Task HandleStatsCommandAsync(
        Message message, 
        User user, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.Text))
            goto invalid_args;
        
        var args = message.Text.Split(' ');
        TimeSpan period;
        if (args.Length < 1)
            goto invalid_args;
        
        switch (args[0])
        {
            case "h":
                period = TimeSpan.FromHours(1);
                break;
            case "d":
                period = TimeSpan.FromDays(1);
                break;
            case "m":
                period = TimeSpan.FromDays(30);
                break;
            case "y":
                period = TimeSpan.FromDays(365);
                break;
            default: goto invalid_args;
        }

        var target = user;
        if (message.ReplyToMessage != null)
            target = message.ReplyToMessage.From;

        if (target == null)
        {
            await _bot.SendMessage(
                chatId: message.Chat.Id,
                text: "Неизвестный пользователь.",
                replyParameters: new() { MessageId = message.MessageId, ChatId = message.Chat.Id },
                cancellationToken: cancellationToken,
                ephemeralMessageParameters: new() { ReceiverUserId = user.Id });
            
            return;
        }

        int? threadId = null;
        if (!(args.Length > 2 && args[1] == "sg") && message.Chat.Type == ChatType.Supergroup)
            threadId = message.MessageThreadId;

        var count = await _repository.CountAsync(message.Chat.Id, threadId, target.Id, period, cancellationToken);

        var periodText = FormatPeriod(period);
        var userName = target.Username is not null ? $"@{target.Username}" : target.FirstName;

        await _bot.SendMessage(
            chatId: message.Chat.Id,
            text: $"{userName} за последние {periodText} написал {count} сообщений.",
            replyParameters: new() { MessageId = message.MessageId, ChatId = message.Chat.Id },
            cancellationToken: cancellationToken,
            disableNotification: true);
        return;
        
        invalid_args:
        await _bot.SendMessage(
            chatId: message.Chat.Id,
            text: "Указано неверное кол-во параметров: используйте /stats [h/d/m/y] [sg].\n" +
                  "Для просмотра статистики конкретного пользователя отправьте команду ответом на сообщение этого пользователя",
            replyParameters: new() { MessageId = message.MessageId, ChatId = message.Chat.Id },
            cancellationToken: cancellationToken,
            ephemeralMessageParameters: new() { ReceiverUserId = user.Id });
    }

    private static string FormatPeriod(TimeSpan ts)
    {
        if (ts.TotalDays >= 30) return $"{ts.TotalDays / 30:F0} мес.";
        if (ts.TotalDays >= 1) return $"{ts.TotalDays:F0} дн.";
        return $"{ts.TotalHours:F0} ч.";
    }
}
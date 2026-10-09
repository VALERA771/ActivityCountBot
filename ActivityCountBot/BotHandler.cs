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
        var period = ParsePeriod(message.Text);

        var count = await _repository.CountAsync(message.Chat.Id, user.Id, period, cancellationToken);

        var periodText = FormatPeriod(period);
        var userName = user.Username is not null ? $"@{user.Username}" : user.FirstName;

        await _bot.SendMessage(
            chatId: message.Chat.Id,
            text: $"{userName}, за последние {periodText} ты написал {count} сообщений.",
            replyParameters: new() { MessageId = message.MessageId, ChatId = message.Chat.Id },
            cancellationToken: cancellationToken);
    }

    private static TimeSpan ParsePeriod(string? text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text ?? "", @"(\d+)(d|mo|h)");
        if (!match.Success) return TimeSpan.FromDays(7);

        var value = int.Parse(match.Groups[1].Value);
        return match.Groups[2].Value switch
        {
            "h" => TimeSpan.FromHours(value),
            "d" => TimeSpan.FromDays(value),
            "mo" => TimeSpan.FromDays(value * 30),
            _ => TimeSpan.FromDays(7)
        };
    }

    private static string FormatPeriod(TimeSpan ts)
    {
        if (ts.TotalDays >= 30) return $"{ts.TotalDays / 30:F0} мес.";
        if (ts.TotalDays >= 1) return $"{ts.TotalDays:F0} дн.";
        return $"{ts.TotalHours:F0} ч.";
    }
}
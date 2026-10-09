using System.Threading.Channels;
using ActivityCountBot.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityCountBot;

public class MessageRepository : IHostedService, IDisposable
{
    private readonly Channel<MessageEntry> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageRepository> _logger;
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts = new();

    public MessageRepository(
        IServiceScopeFactory scopeFactory,
        ILogger<MessageRepository> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _channel = Channel.CreateBounded<MessageEntry>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = false,
                SingleReader = true
            });

        _consumerTask = Task.Run(() => ConsumeLoop(_cts.Token));
    }

    public async ValueTask EnqueueAsync(MessageEntry message, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(message, ct);
    }

    public async Task<int> CountAsync(long chatId, long userId, TimeSpan period, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var since = DateTime.UtcNow - period;

        return await db.Messages
            .Where(m => m.ChatId == chatId && m.UserId == userId && m.SentAt >= since)
            .CountAsync(ct);
    }

    private async Task ConsumeLoop(CancellationToken ct)
    {
        var batch = new List<MessageEntry>(500);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();

                while (batch.Count < 500 && _channel.Reader.TryRead(out var item))
                    batch.Add(item);

                if (batch.Count == 0) continue;

                await FlushBatchAsync(batch, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumer loop crashed");
        }
    }

    private async Task FlushBatchAsync(List<MessageEntry> batch, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Messages.AddRange(batch);
            await db.SaveChangesAsync(ct);

            _logger.LogDebug("Flushed {Count} messages to DB", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch of {Count} messages", batch.Count);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MessageRepository: stopping, flushing remaining messages...");
        _channel.Writer.Complete();

        try
        {
            await _consumerTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("MessageRepository: consumer did not finish in time");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        
        try
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        catch (ObjectDisposedException) { }
    }
}
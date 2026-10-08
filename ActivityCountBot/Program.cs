using ActivityCountBot.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace ActivityCountBot;

public class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("BotDb")
                                       ?? throw new InvalidOperationException("ConnectionStrings__BotDb is not set");

                // Регистрируем DbContext с Pomelo
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseMySql(
                        connectionString,
                        ServerVersion.AutoDetect(connectionString),  // или явно: new MySqlServerVersion(new Version(8, 4, 0))
                        mySqlOptions =>
                        {
                            mySqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 10,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                        });
                });

                services.AddSingleton(new TelegramBotClient(
                    context.Configuration["BotSettings:Token"]
                    ?? throw new InvalidOperationException("BotSettings__Token is not set")));

                services.AddSingleton<BotHandler>();
                
                services.AddSingleton<MessageRepository>();
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MessageRepository>());

                services.AddHostedService<BotWorker>();
            })
            .Build();
        
        if (args.Contains("--migrate"))
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            Console.WriteLine("[DB] Migrations successfully applied. Exiting...");
            return;
        }
        
        await host.RunAsync();
    }
}
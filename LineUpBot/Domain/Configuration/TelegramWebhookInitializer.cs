
using Telegram.Bot;

namespace LineUpBot.Domain.Configuration
{
    public class TelegramWebhookInitializer : IHostedService
    {
        private readonly ITelegramBotClient _botClient;

        public TelegramWebhookInitializer(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _botClient.SetWebhook(
                url: "https://38e75f6aa0eb.ngrok-free.app/webhook",
                cancellationToken: cancellationToken
            );
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

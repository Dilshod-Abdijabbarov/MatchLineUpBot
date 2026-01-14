
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
                url: "https://p01--matchlineup--t9sr8pzzydnp.code.run/api/webhook",
                cancellationToken: cancellationToken
            );
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

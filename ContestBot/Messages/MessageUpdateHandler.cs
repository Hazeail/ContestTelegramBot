using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Messages
{
    internal sealed class MessageUpdateHandler
    {
        private readonly MessageRouter _router;

        public MessageUpdateHandler(MessageRouter router)
        {
            _router = router;
        }

        public Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
            => _router.TryHandleAsync(bot, msg, text, ct);
    }
}

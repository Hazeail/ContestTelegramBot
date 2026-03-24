using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Messages.Actions
{
    internal sealed class AdminChannelsMessagesAction : IMessageAction
    {
        private readonly Func<long, bool> _isAdmin;
        private readonly Func<ITelegramBotClient, Message, string, CancellationToken, Task<bool>> _handler;

        public AdminChannelsMessagesAction(
            Func<long, bool> isAdmin,
            Func<ITelegramBotClient, Message, string, CancellationToken, Task<bool>> handler)
        {
            _isAdmin = isAdmin ?? throw new ArgumentNullException(nameof(isAdmin));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            if (msg?.From == null) return Task.FromResult(false);
            if (!_isAdmin(msg.From.Id)) return Task.FromResult(false);

            text = (text ?? string.Empty).Trim();
            return _handler(bot, msg, text, ct);
        }
    }
}
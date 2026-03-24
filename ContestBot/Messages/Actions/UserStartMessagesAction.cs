using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Messages.Actions
{
    internal sealed class UserStartMessagesAction : IMessageAction
    {
        private readonly Func<ITelegramBotClient, Message, CancellationToken, Task> _handleStartAsync;

        public UserStartMessagesAction(Func<ITelegramBotClient, Message, CancellationToken, Task> handleStartAsync)
        {
            _handleStartAsync = handleStartAsync;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            if (msg == null) return false;
            if (!string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
                return false;

            await _handleStartAsync(bot, msg, ct);
            return true;
        }
    }
}

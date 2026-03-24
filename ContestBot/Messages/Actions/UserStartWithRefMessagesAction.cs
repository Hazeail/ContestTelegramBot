using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Messages.Actions
{
    internal sealed class UserStartWithRefMessagesAction : IMessageAction
    {
        private readonly Func<ITelegramBotClient, Message, string, CancellationToken, Task> _handleStartWithArgAsync;

        public UserStartWithRefMessagesAction(Func<ITelegramBotClient, Message, string, CancellationToken, Task> handleStartWithArgAsync)
        {
            _handleStartWithArgAsync = handleStartWithArgAsync;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            if (msg == null) return false;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim();

            // "/start" с аргументом: "/start 123" или "/start abc"
            if (!text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
                return false;

            // строго отделяем чистый "/start" (его уже ловит другой action)
            if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
                return false;

            // берём аргумент после "/start"
            // варианты: "/start 123", "/start    123"
            var parts = text.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            string arg = parts[1].Trim();
            if (arg.Length == 0) return false;

            await _handleStartWithArgAsync(bot, msg, arg, ct);
            return true;
        }
    }
}

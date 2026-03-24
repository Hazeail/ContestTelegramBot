using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Admin.Creation;

namespace ContestBot.Messages.Actions
{
    internal sealed class AdminContestCreationMessagesAction : IMessageAction
    {
        private readonly Func<long, bool> _isAdmin;
        private readonly ContestCreationStore _store;
        private readonly Func<ITelegramBotClient, Message, string, CancellationToken, Task> _handleCreationStepAsync;

        public AdminContestCreationMessagesAction(
            Func<long, bool> isAdmin,
            ContestCreationStore store,
            Func<ITelegramBotClient, Message, string, CancellationToken, Task> handleCreationStepAsync)
        {
            _isAdmin = isAdmin ?? throw new ArgumentNullException(nameof(isAdmin));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _handleCreationStepAsync = handleCreationStepAsync ?? throw new ArgumentNullException(nameof(handleCreationStepAsync));
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            if (msg == null || msg.From == null) return false;
            if (!_isAdmin(msg.From.Id)) return false;

            var session = _store.TryGet(msg.From.Id);
            if (session == null || !session.IsActive || session.Draft == null)
                return false;

            text = (text ?? string.Empty).Trim();

            // шаги конструктора — только если это не команда
            bool isCommand = !string.IsNullOrEmpty(text) && text.StartsWith("/");
            bool hasMedia =
                (msg.Photo != null && msg.Photo.Length > 0) ||
                (msg.Animation != null) ||
                (msg.Video != null);

            if (!isCommand && (!string.IsNullOrEmpty(text) || hasMedia))
            {
                await _handleCreationStepAsync(bot, msg, text, ct);
                return true;
            }

            return false;
        }
    }
}
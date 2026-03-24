using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Admin.Manage;

namespace ContestBot.Messages.Actions
{
    internal sealed class AdminContestManageMessagesAction : IMessageAction
    {
        private readonly Func<long, bool> _isAdmin;
        private readonly ContestManageStore _store;
        private readonly Func<ITelegramBotClient, Message, string, CancellationToken, Task> _handleAsync;

        public AdminContestManageMessagesAction(
            Func<long, bool> isAdmin,
            ContestManageStore store,
            Func<ITelegramBotClient, Message, string, CancellationToken, Task> handleAsync)
        {
            _isAdmin = isAdmin ?? throw new ArgumentNullException(nameof(isAdmin));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _handleAsync = handleAsync ?? throw new ArgumentNullException(nameof(handleAsync));
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            if (msg == null || msg.From == null) return false;
            if (!_isAdmin(msg.From.Id)) return false;

            var s = _store.TryGet(msg.From.Id);
            if (s == null || !s.IsActive) return false;

            text = (text ?? string.Empty).Trim();
            bool isCommand = !string.IsNullOrEmpty(text) && text.StartsWith("/");

            bool isWaitMedia = s.State == ContestManageState.WaitMedia;

            bool hasMedia =
                msg.Type == Telegram.Bot.Types.Enums.MessageType.Photo ||
                msg.Type == Telegram.Bot.Types.Enums.MessageType.Video ||
                msg.Type == Telegram.Bot.Types.Enums.MessageType.Animation;

            if (!isCommand && (!string.IsNullOrEmpty(text) || (isWaitMedia && hasMedia)))
            {
                await _handleAsync(bot, msg, text, ct);
                return true;
            }

            return false;
        }
    }
}
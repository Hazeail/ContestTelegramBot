using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Admin.Creation;
using ContestBot.Services;
using ContestBot.Channels;
using ContestBot.Admin.Manage;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction : ICallbackAction
    {
        private readonly Func<long, bool> _isAdmin;
        private readonly ContestCreationStore _creationStore;

        private readonly Func<int> _getNextContestId;

        private readonly Func<InlineKeyboardMarkup> _buildAdminMenuKb;
        private readonly Func<InlineKeyboardMarkup> _buildAdminCreateTypeKb;
        private readonly Func<InlineKeyboardMarkup> _buildAdminCreateCancelKb;
        private readonly Func<InlineKeyboardMarkup> _buildAdminReferralPresetKb;
        private readonly Func<InlineKeyboardMarkup> _buildSkipMediaKb;
        private readonly Func<int, InlineKeyboardMarkup> _buildWinnersCountKb;
        private readonly Func<IReadOnlyList<ChannelInfo>, InlineKeyboardMarkup> _buildAdminCreateChannelKb;
        private readonly Func<InlineKeyboardMarkup> _buildAdminCreateEditMenuKb;
        private readonly Func<InlineKeyboardMarkup> _buildAdminEditTypeKb;
        private readonly Func<InlineKeyboardMarkup> _buildAdminEditReferralPresetKb;

        private readonly ContestManageStore _manageStore;
        private readonly Func<ITelegramBotClient, long, int, CancellationToken, Task> _showAdminMenuAsync;

        private readonly Func<ITelegramBotClient, long, int, string, InlineKeyboardMarkup, CancellationToken, Task<int>> _deleteAndSendHtmlAsync;

        private readonly Func<ITelegramBotClient, Contest, CancellationToken, Task> _publishContestToChannelAsync;
        private readonly Func<ITelegramBotClient, long, string, CancellationToken, Task> _sendPrivateAsync;
        private readonly Func<ITelegramBotClient, Contest, CancellationToken, Task> _tryUpdateChannelPostAsync;
        private readonly Func<ITelegramBotClient, Contest, CancellationToken, Task<ChannelPostUpdateResult>> _repostChannelPostWithResultAsync;

        private readonly long _superAdminUserId;
        private readonly Func<ITelegramBotClient, long, long, int, string, CancellationToken, Task> _showCreationPreviewAsync;

        private readonly OwnerNotificationService _ownerNotify;

        public AdminCallbacksAction(
            Func<long, bool> isAdmin,
            ContestCreationStore creationStore,
            Func<int> getNextContestId,
            ContestManageStore manageStore,

            Func<InlineKeyboardMarkup> buildAdminMenuKb,
            Func<InlineKeyboardMarkup> buildAdminCreateTypeKb,
            Func<InlineKeyboardMarkup> buildAdminCreateCancelKb,
            Func<InlineKeyboardMarkup> buildAdminReferralPresetKb,
            Func<InlineKeyboardMarkup> buildSkipMediaKb,
            Func<int, InlineKeyboardMarkup> buildWinnersCountKb,
            Func<IReadOnlyList<ChannelInfo>, InlineKeyboardMarkup> buildAdminCreateChannelKb,

            Func<InlineKeyboardMarkup> buildAdminCreateEditMenuKb,
            Func<InlineKeyboardMarkup> buildAdminEditTypeKb,
            Func<InlineKeyboardMarkup> buildAdminEditReferralPresetKb,

            Func<ITelegramBotClient, long, int, CancellationToken, Task> showAdminMenuAsync,
            Func<ITelegramBotClient, long, int, string, InlineKeyboardMarkup, CancellationToken, Task<int>> deleteAndSendHtmlAsync,
            Func<ITelegramBotClient, Contest, CancellationToken, Task> publishContestToChannelAsync,
            Func<ITelegramBotClient, long, string, CancellationToken, Task> sendPrivateAsync,
            Func<ITelegramBotClient, Contest, CancellationToken, Task> tryUpdateChannelPostAsync,
            Func<ITelegramBotClient, Contest, CancellationToken, Task<ChannelPostUpdateResult>> repostChannelPostWithResultAsync,

            long superAdminUserId,
            Func<ITelegramBotClient, long, long, int, string, CancellationToken, Task> showCreationPreviewAsync)
        {
            _isAdmin = isAdmin ?? throw new ArgumentNullException(nameof(isAdmin));
            _creationStore = creationStore ?? throw new ArgumentNullException(nameof(creationStore));
            _getNextContestId = getNextContestId ?? throw new ArgumentNullException(nameof(getNextContestId));
            _manageStore = manageStore ?? throw new ArgumentNullException(nameof(manageStore));

            _buildAdminMenuKb = buildAdminMenuKb ?? throw new ArgumentNullException(nameof(buildAdminMenuKb));
            _buildAdminCreateTypeKb = buildAdminCreateTypeKb ?? throw new ArgumentNullException(nameof(buildAdminCreateTypeKb));
            _buildAdminCreateCancelKb = buildAdminCreateCancelKb ?? throw new ArgumentNullException(nameof(buildAdminCreateCancelKb)); _buildAdminReferralPresetKb = buildAdminReferralPresetKb ?? throw new ArgumentNullException(nameof(buildAdminReferralPresetKb));
            _buildSkipMediaKb = buildSkipMediaKb ?? throw new ArgumentNullException(nameof(buildSkipMediaKb));
            _buildWinnersCountKb = buildWinnersCountKb ?? throw new ArgumentNullException(nameof(buildWinnersCountKb));
            _buildAdminCreateChannelKb = buildAdminCreateChannelKb ?? throw new ArgumentNullException(nameof(buildAdminCreateChannelKb));

            _buildAdminCreateEditMenuKb = buildAdminCreateEditMenuKb ?? throw new ArgumentNullException(nameof(buildAdminCreateEditMenuKb));
            _buildAdminEditTypeKb = buildAdminEditTypeKb ?? throw new ArgumentNullException(nameof(buildAdminEditTypeKb));
            _buildAdminEditReferralPresetKb = buildAdminEditReferralPresetKb ?? throw new ArgumentNullException(nameof(buildAdminEditReferralPresetKb));

            _showAdminMenuAsync = showAdminMenuAsync ?? throw new ArgumentNullException(nameof(showAdminMenuAsync));

            _deleteAndSendHtmlAsync = deleteAndSendHtmlAsync ?? throw new ArgumentNullException(nameof(deleteAndSendHtmlAsync));

            _publishContestToChannelAsync = publishContestToChannelAsync ?? throw new ArgumentNullException(nameof(publishContestToChannelAsync));
            _sendPrivateAsync = sendPrivateAsync ?? throw new ArgumentNullException(nameof(sendPrivateAsync));
            _tryUpdateChannelPostAsync = tryUpdateChannelPostAsync ?? throw new ArgumentNullException(nameof(tryUpdateChannelPostAsync));
            _repostChannelPostWithResultAsync = repostChannelPostWithResultAsync ?? throw new ArgumentNullException(nameof(repostChannelPostWithResultAsync));

            _superAdminUserId = superAdminUserId;
            _showCreationPreviewAsync = showCreationPreviewAsync ?? throw new ArgumentNullException(nameof(showCreationPreviewAsync));

            _ownerNotify = new OwnerNotificationService(_sendPrivateAsync, _superAdminUserId, _isAdmin);
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq == null || cq.From == null) return false;

            // UX: всегда закрываем “крутилку” на inline-кнопках админки.
            // Важно: ответить нужно до тяжёлой логики.
            // Дополнительно: если не-админ нажал admin:* кнопку (старое сообщение/пересылка) — тоже закрываем callback.
            if (!string.IsNullOrEmpty(cq.Data) && cq.Data.StartsWith("admin:", StringComparison.OrdinalIgnoreCase))
            {
                if (!_isAdmin(cq.From.Id))
                {
                    await SafeAnswer(bot, cq.Id, "Недостаточно прав.", ct);
                    return true;
                }

                await SafeAnswer(bot, cq.Id, null, ct);
            }

            if (!_isAdmin(cq.From.Id)) return false;

            long adminId = cq.From.Id;
            long chatId = cq.Message != null ? cq.Message.Chat.Id : adminId;

            // Важно: session на админа (в твоём коде это ContestCreationStore.Session)
            var session = _creationStore.GetOrCreate(adminId);

            // Якорь админки:
            // В callback всегда приоритет у сообщения, на котором нажали кнопку (cq.Message.MessageId).
            int msgId = 0;

            if (cq.Message != null)
            {
                msgId = cq.Message.MessageId;
                session.PanelMessageId = msgId; // фиксируем якорь на актуальную панель
            }
            else if (session.PanelMessageId.HasValue)
            {
                msgId = session.PanelMessageId.Value; // fallback, если cq.Message вдруг отсутствует
            }

            // порядок важен: сначала меню/закрытие, потом конструктор
            if (await TryHandleMenuAsync(bot, cq, chatId, msgId, ct)) return true;
            if (await TryHandleCreateFlowAsync(bot, cq, chatId, msgId, ct)) return true;
            if (await TryHandleChannelPickAsync(bot, cq, chatId, msgId, ct)) return true;
            if (await TryHandleWinnersAsync(bot, cq, chatId, msgId, ct)) return true;
            if (await TryHandlePublishAsync(bot, cq, chatId, msgId, ct)) return true;

            return false;
        }
    }
}
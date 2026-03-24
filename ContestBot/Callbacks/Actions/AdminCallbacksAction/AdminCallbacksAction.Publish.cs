using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Telegram.Bot;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private async Task<bool> TryHandlePublishAsync(
            ITelegramBotClient bot,
            Telegram.Bot.Types.CallbackQuery cq,
            long chatId,
            int msgId,
            CancellationToken ct)
        {
            if (cq.Data != "admin:create_publish")
                return false;

            long adminId = cq.From != null ? cq.From.Id : 0;
            var session = _creationStore.GetOrCreate(adminId);

            var draft = session.Draft;

            if (draft == null || string.IsNullOrWhiteSpace(draft.Name))
            {
                session.Reset();

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "Черновик не готов. Начни создание заново.",
                    _buildAdminMenuKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (!draft.ChannelId.HasValue)
            {
                var channels = Database.LoadActiveChannels();
                int pickId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "Перед публикацией выбери канал:",
                    _buildAdminCreateChannelKb(channels),
                    ct);

                session.PanelMessageId = pickId;
                return true;
            }

            try
            {
                if (draft.Id <= 0)
                    draft.Id = _getNextContestId();

                await _publishContestToChannelAsync(bot, draft, ct);

                string channelText =
                    !string.IsNullOrWhiteSpace(draft.ChannelUsername) ? ("@" + draft.ChannelUsername.TrimStart('@')) :
                    (draft.ChannelId.HasValue ? draft.ChannelId.Value.ToString() : "неизвестно");

                string when = draft.EndAt.ToString("dd.MM.yyyy HH:mm");

                DeactivateDraftInDb(session, adminId);

                session.Reset();

                string safeName = WebUtility.HtmlEncode(draft.Name ?? "");
                string safeChannel = WebUtility.HtmlEncode(channelText);
                string safeWhen = WebUtility.HtmlEncode(when);

                string html =
                    "<b>✅ Конкурс опубликован</b>\n\n" +
                    "<b>" + safeName + "</b>\n" +
                    "Канал: <code>" + safeChannel + "</code>\n" +
                    "Розыгрыш: <code>" + safeWhen + "</code>";

                int doneId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    html,
                    _buildAdminMenuKb(),
                    ct);

                session.PanelMessageId = doneId;
                return true;
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                await _showCreationPreviewAsync(
                    bot, chatId, adminId, msgId,
                    "Не получилось опубликовать конкурс. Проверь права бота в канале и попробуй ещё раз.\n\nПричина: " + (ex.Message ?? ""),
                    ct);

                string channelText =
                    !string.IsNullOrWhiteSpace(draft.ChannelUsername) ? ("@" + draft.ChannelUsername.TrimStart('@')) :
                    (draft.ChannelId.HasValue ? draft.ChannelId.Value.ToString() : "неизвестно");

                return true;
            }
            catch (Exception ex)
            {
                await _showCreationPreviewAsync(
                    bot, chatId, adminId, msgId,
                    "Не получилось опубликовать конкурс. Попробуй ещё раз или выбери другой канал.\n\nПричина: " + (ex.Message ?? ""),
                    ct);

                return true;
            }
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private async Task<bool> TryHandleChannelPickAsync(
            ITelegramBotClient bot,
            Telegram.Bot.Types.CallbackQuery cq,
            long chatId,
            int msgId,
            CancellationToken ct)
        {
            long adminId = cq.From != null ? cq.From.Id : 0;
            var session = _creationStore.GetOrCreate(adminId);

            if (cq.Data == "admin:create_pick_channel")
            {
                var channels = Database.LoadActiveChannels();

                if (channels == null || channels.Count == 0)
                {
                    int panelIdEmpty = await _deleteAndSendHtmlAsync(
                        bot, chatId, msgId,
                        "Нет активных каналов.\n\nДобавь канал в разделе «Каналы», затем вернись сюда.",
                        _buildAdminMenuKb(),
                        ct);

                    session.PanelMessageId = panelIdEmpty;
                    return true;
                }

                int pickId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "Выбери канал для публикации конкурса:",
                    _buildAdminCreateChannelKb(channels),
                    ct);

                session.PanelMessageId = pickId;
                return true;
            }

            if (cq.Data.StartsWith("admin:create_channel:", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Draft == null) return true;

                var parts = cq.Data.Split(':');
                if (parts.Length == 3)
                {
                    long channelId;
                    if (long.TryParse(parts[2], out channelId))
                    {
                        var channels = Database.LoadActiveChannels();
                        var picked = channels != null ? channels.Find(x => x.ChannelId == channelId) : null;

                        session.Draft.ChannelId = channelId;
                        session.Draft.ChannelUsername = picked != null ? picked.Username : null;
                        SaveDraftToDb(session, adminId);

                        await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                        return true;
                    }
                }

                var fallback = Database.LoadActiveChannels();
                int fallbackId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "Не понял выбор канала. Выбери канал ещё раз:",
                    _buildAdminCreateChannelKb(fallback),
                    ct);

                session.PanelMessageId = fallbackId;
                return true;
            }

            return false;
        }
    }
}
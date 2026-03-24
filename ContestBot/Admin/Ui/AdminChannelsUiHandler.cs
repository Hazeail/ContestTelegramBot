using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContestBot.Channels;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot.Admin.Ui
{
    internal sealed class AdminChannelsUiHandler
    {
        private readonly Func<InlineKeyboardMarkup> _buildChannelsKb;
        private readonly Func<InlineKeyboardMarkup> _buildWaitKb;
        private readonly Func<IReadOnlyList<ChannelInfo>, InlineKeyboardMarkup> _buildDisablePickKb;

        public AdminChannelsUiHandler(
            Func<InlineKeyboardMarkup> buildChannelsKb,
            Func<InlineKeyboardMarkup> buildWaitKb,
            Func<IReadOnlyList<ChannelInfo>, InlineKeyboardMarkup> buildDisablePickKb)
        {
            _buildChannelsKb = buildChannelsKb;
            _buildWaitKb = buildWaitKb;
            _buildDisablePickKb = buildDisablePickKb;
        }

        public Task ShowChannelsListAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? editMessageId,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
            CancellationToken token)
        {
            var channels = Database.LoadActiveChannels();

            var sb = new StringBuilder();
            sb.AppendLine("<b>Каналы</b>");
            sb.AppendLine();
            sb.AppendLine("• <b>Добавить</b> — перешли пост из канала");
            sb.AppendLine("• <b>Отключить</b> — выбери из списка");
            sb.AppendLine();
            sb.AppendLine("<b>Список</b>");

            if (channels.Count == 0)
            {
                sb.AppendLine("<i>Пусто.</i>");
            }
            else
            {
                foreach (var c in channels)
                {
                    string title = c?.GetDisplayName();
                    if (string.IsNullOrWhiteSpace(title))
                        title = c?.ChannelId.ToString() ?? "";

                    sb.AppendLine("• " + title);
                }
            }

            return sendOrEditHtmlAsync(botClient, chatId, editMessageId, sb.ToString(), _buildChannelsKb(), token);
        }

        public Task ShowAddInstructionAsync(
             ITelegramBotClient botClient,
             long chatId,
             int? editMessageId,
             string problem,
             Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
             CancellationToken token)
        {
            var prefix = string.IsNullOrWhiteSpace(problem)
                ? ""
                : "<i>" + problem + "</i>\n\n";

            string text =
                "<b>Добавить канал</b>\n\n" +
                prefix +
                "Открой нужный канал → выбери любой пост → нажми «Переслать» → отправь сюда.\n" +
                "Если не получается — проверь, что бот добавлен в канал и имеет доступ к постам.";

            return sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, _buildWaitKb(), token);
        }

        public Task ShowDisablePickListAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? editMessageId,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
            CancellationToken token)
        {
            var channels = Database.LoadActiveChannels();

            string text =
                "<b>Отключить канал</b>\n\n" +
                "Выбери канал:";

            return sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, _buildDisablePickKb(channels), token);
        }

    }
}
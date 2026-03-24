using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContestBot.Admins;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot.Admin.Ui
{
    internal sealed class AdminAdminsUiHandler
    {
        private readonly long _superAdminUserId;
        private readonly Func<InlineKeyboardMarkup> _buildAdminsKb;
        private readonly Func<InlineKeyboardMarkup> _buildWaitKb;
        private readonly Func<IReadOnlyList<AdminProfile>, InlineKeyboardMarkup> _buildDisablePickKb;

        public AdminAdminsUiHandler(
            long superAdminUserId,
            Func<InlineKeyboardMarkup> buildAdminsKb,
            Func<InlineKeyboardMarkup> buildWaitKb,
            Func<IReadOnlyList<AdminProfile>, InlineKeyboardMarkup> buildDisablePickKb)
        {
            _superAdminUserId = superAdminUserId;
            _buildAdminsKb = buildAdminsKb;
            _buildWaitKb = buildWaitKb;
            _buildDisablePickKb = buildDisablePickKb;
        }

        public Task ShowAdminsListAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? editMessageId,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
            CancellationToken token)
        {
            var admins = Database.LoadActiveAdminProfiles();
            admins.RemoveAll(a => a.UserId == _superAdminUserId);

            var sb = new StringBuilder();
            sb.AppendLine("<b>Админы</b>");
            sb.AppendLine();
            sb.AppendLine("• <b>Добавить</b> — перешли сообщение пользователя");
            sb.AppendLine("• <b>Отключить</b> — выбери из списка");
            sb.AppendLine();
            sb.AppendLine("<b>Список</b>");

            if (admins.Count == 0)
            {
                sb.AppendLine("<i>Пусто.</i>");
            }
            else
            {
                foreach (var a in admins)
                {
                    string title = a?.GetDisplayName();
                    if (string.IsNullOrWhiteSpace(title))
                        title = a?.UserId.ToString() ?? "";

                    // чуть безопаснее визуально
                    sb.AppendLine("• "+ title);
                }
            }

            return sendOrEditHtmlAsync(botClient, chatId, editMessageId, sb.ToString(), _buildAdminsKb(), token);
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
                "<b>Добавить админа</b>\n\n" +
                prefix +
                "Открой диалог с человеком → выбери любое его сообщение → нажми «Переслать» → отправь сюда.\n" +
                "Мне важно видеть автора сообщения.";

            return sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, _buildWaitKb(), token);
        }


        public Task ShowDisablePickListAsync(
             ITelegramBotClient botClient,
             long chatId,
             int? editMessageId,
             Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
             CancellationToken token)
        {
            var admins = Database.LoadActiveAdminProfiles();
            admins.RemoveAll(a => a.UserId == _superAdminUserId);

            string text =
                "<b>Отключить админа</b>\n\n" +
                "Выбери админа:";

            return sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, _buildDisablePickKb(admins), token);
        }

    }
}

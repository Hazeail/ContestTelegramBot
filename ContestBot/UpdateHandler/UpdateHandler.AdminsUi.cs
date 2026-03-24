using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ContestBot
{
    internal sealed partial class UpdateHandler
    {
        // =========================
        //  ADMINS UI WRAPPERS
        // =========================
        private Task ShowAdminsListAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return _adminAdminsUi.ShowAdminsListAsync(botClient, chatId, editMessageId, _root.Ui.SendOrEditHtmlAsync, token);
        }

        private Task ShowAdminsAddInstructionAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return ShowAdminsAddInstructionAsync(botClient, chatId, editMessageId, null, token);
        }

        private Task ShowAdminsAddInstructionAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, string problem, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return _adminAdminsUi.ShowAddInstructionAsync(botClient, chatId, editMessageId, problem, _root.Ui.SendOrEditHtmlAsync, token);
        }

        private Task ShowAdminsDisablePickListAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return _adminAdminsUi.ShowDisablePickListAsync(botClient, chatId, editMessageId, _root.Ui.SendOrEditHtmlAsync, token);
        }

    }
}

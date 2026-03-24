using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ContestBot
{
    internal sealed partial class UpdateHandler
    {
        // =========================
        //  CHANNELS UI WRAPPERS
        // =========================
        private Task ShowChannelsListAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return _adminChannelsUi.ShowChannelsListAsync(botClient, chatId, editMessageId, _root.Ui.SendOrEditHtmlAsync, token);
        }

        private Task ShowChannelsAddInstructionAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return ShowChannelsAddInstructionAsync(botClient, chatId, editMessageId, null, token);
        }

        private Task ShowChannelsAddInstructionAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, string problem, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return _adminChannelsUi.ShowAddInstructionAsync(botClient, chatId, editMessageId, problem, _root.Ui.SendOrEditHtmlAsync, token);
        }

        private Task ShowChannelsDisablePickListAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            if (editMessageId.HasValue) _adminPanelMessageId = editMessageId.Value;
            return _adminChannelsUi.ShowDisablePickListAsync(botClient, chatId, editMessageId, _root.Ui.SendOrEditHtmlAsync, token);
        }

    }
}
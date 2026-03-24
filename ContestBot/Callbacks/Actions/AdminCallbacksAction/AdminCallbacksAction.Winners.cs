using System.Threading;
using System.Threading.Tasks;
using System;
using Telegram.Bot;
using ContestBot.Admin.Creation;
using ContestBot.Utils;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private async Task<bool> TryHandleWinnersAsync(
            ITelegramBotClient bot,
            Telegram.Bot.Types.CallbackQuery cq,
            long chatId,
            int msgId,
            CancellationToken ct)
        {
            if (!CallbackDataParser.TryParseWinners(cq.Data, out WinnersActionKind winnersAction))
                return false;

            long adminId = cq.From != null ? cq.From.Id : 0;
            var session = _creationStore.GetOrCreate(adminId);
            var now = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            if (session.Draft == null) return true;

            bool isEdit = (session.State == ContestCreationState.EditWinnersCount);
            if (!isEdit)
                session.State = ContestCreationState.WaitWinnersCount;


            int current = ContestCreationRules.EnsureWinnersInitialized(session.Draft.WinnersCount);
            session.Draft.WinnersCount = current;

            switch (winnersAction)
            {
                case WinnersActionKind.Noop:
                    return true;

                case WinnersActionKind.Inc:
                    current = ContestCreationRules.ApplyDelta(current, 1);
                    break;

                case WinnersActionKind.Dec:
                    current = ContestCreationRules.ApplyDelta(current, -1);
                    break;

                case WinnersActionKind.Ok:
                    session.Draft.WinnersCount = current;
                    SaveDraftToDb(session, adminId);

                    if (isEdit)
                    {
                        await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                        return true;
                    }

                    session.State = ContestCreationState.WaitDrawDateTime;
                    SaveDraftToDb(session, adminId);

                    int panelOk = await _deleteAndSendHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Шаг 5/6 • Дата розыгрыша</b>\n\nВведи дату и время розыгрыша.\nФормат: <code>" + now + "</code>",
                        _buildAdminCreateCancelKb(),
                        ct);

                    session.PanelMessageId = panelOk;
                    return true;


                default:
                    return true;
            }

            session.Draft.WinnersCount = current;
            SaveDraftToDb(session, adminId);

            int panelId = await _deleteAndSendHtmlAsync(
                bot, chatId, msgId,
                "<b>Шаг 4/6 • Призовые места</b>\n\n" +
                "Сколько будет призовых мест?\n\n" +
                "Текущее значение: <b>" + current + "</b>\n\n" +
                "Можно кнопками или отправь число сообщением.",
                _buildWinnersCountKb(current),
                ct);

            session.PanelMessageId = panelId;
            return true;
        }
    }
}
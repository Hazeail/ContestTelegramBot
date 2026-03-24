using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using ContestBot.Admin.Creation;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private static async Task SafeAnswer(ITelegramBotClient bot, string cqId, string text, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    await bot.AnswerCallbackQuery(cqId, cacheTime: 0, cancellationToken: ct);
                else
                    await bot.AnswerCallbackQuery(cqId, text, cacheTime: 0, cancellationToken: ct);
            }
            catch { }
        }
        private static void SaveDraftToDb(ContestCreationStore.Session session, long adminId)
        {
            if (session == null) return;
            if (session.Draft == null) return;

            // если DraftId ещё не создан — создаём запись в БД
            if (!session.DraftId.HasValue)
            {
                session.DraftId = Database.CreateContestDraft(adminId, session.State, session.Draft);
                return;
            }

            Database.UpdateContestDraft(session.DraftId.Value, adminId, session.State, session.Draft);
        }

        private static void EnsureDraftId(ContestCreationStore.Session session, long adminId)
        {
            if (session == null) return;
            if (session.Draft == null) return;

            if (!session.DraftId.HasValue)
                session.DraftId = Database.CreateContestDraft(adminId, session.State, session.Draft);
        }

        private static void DeactivateDraftInDb(ContestCreationStore.Session session, long adminId)
        {
            if (session == null) return;
            if (!session.DraftId.HasValue) return;

            Database.DeactivateContestDraft(adminId, session.DraftId.Value);
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Services;

namespace ContestBot.User.Start
{
    internal sealed class StartHandler
    {
        private readonly ContestManager _contestManager;
        private readonly ParticipantsManager _participants;
        private readonly ContestChannelPostsService _channelPosts;
        private readonly ChannelSubscriptionService _subs;

        private readonly Func<long, int?> _getLastContestId;
        private readonly Action<long, int> _setLastContestId;

        private readonly Func<ITelegramBotClient, long, long, Contest, int?, string, CancellationToken, Task> _showUserMenuAsync;
        private readonly Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> _showNeutralStartAsync;
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, string, Task> _showFinishedContestCardAsync;

        public StartHandler(
            ContestManager contestManager,
            ParticipantsManager participants,
            ContestChannelPostsService channelPosts,
            ChannelSubscriptionService subs,
            Func<long, int?> getLastContestId,
            Action<long, int> setLastContestId,
            Func<ITelegramBotClient, long, long, Contest, int?, string, CancellationToken, Task> showUserMenuAsync,
            Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> showNeutralStartAsync,
            Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, string, Task> showFinishedContestCardAsync)
        {
            _contestManager = contestManager;
            _participants = participants;
            _channelPosts = channelPosts;
            _subs = subs;

            _getLastContestId = getLastContestId;
            _setLastContestId = setLastContestId;

            _showUserMenuAsync = showUserMenuAsync;
            _showNeutralStartAsync = showNeutralStartAsync;
            _showFinishedContestCardAsync = showFinishedContestCardAsync;
        }

        public Task HandleStartWithArgAsync(ITelegramBotClient botClient, Message msg, string arg, CancellationToken token)
        {
            // Вся логика payload уже внутри HandleStartAsync по msg.Text
            return HandleStartAsync(botClient, msg, token);
        }

        public async Task HandleStartAsync(ITelegramBotClient botClient, Message msg, CancellationToken token)
        {
            if (msg?.From == null) return;

            long chatId = msg.Chat.Id;
            long userId = msg.From.Id;
            string username = msg.From.Username;
            string text = msg.Text ?? string.Empty;

            string payload = ExtractPayload(text);

            // /start без payload — не дублируем WELCOME.
            // Показываем нейтральный старт (экран 9) и навигацию "Сменить конкурс".
            if (string.IsNullOrWhiteSpace(payload))
            {
                await _showNeutralStartAsync(botClient, chatId, null, null, token);
                return;
            }

            // parsed payload
            bool isReferralPayload = false;
            string contestCodeFromReferral = null;
            long inviterIdFromReferral = 0;

            int? contestIdFromCid = null;

            if (!string.IsNullOrEmpty(payload))
            {
                if (payload.StartsWith("ref_", StringComparison.OrdinalIgnoreCase))
                {
                    // ref_{contestCode}_{inviterId}
                    var p = payload.Split('_');
                    if (p.Length == 3 && !string.IsNullOrWhiteSpace(p[1]) && long.TryParse(p[2], out var parsedInviter))
                    {
                        isReferralPayload = true;
                        contestCodeFromReferral = p[1];
                        inviterIdFromReferral = parsedInviter;
                    }
                }
                else if (payload.StartsWith("cid_", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(payload.Substring(4), out int id))
                        contestIdFromCid = id;
                }
            }

            // === Resolve contest + optional inviter ===
            Contest contest = null;
            long? inviterId = null;

            if (isReferralPayload)
            {
                contest = _contestManager.GetContestByCode(contestCodeFromReferral);
                if (inviterIdFromReferral != userId)
                    inviterId = inviterIdFromReferral;
            }
            else if (contestIdFromCid.HasValue)
            {
                contest = _contestManager.GetContestById(contestIdFromCid.Value);
            }
            else
            {
                var lastContestId = _getLastContestId(userId);
                if (lastContestId.HasValue)
                    contest = _contestManager.GetContestById(lastContestId.Value);
            }

            // === If contest not found ===
            if (contest == null)
            {
                string reason = !string.IsNullOrEmpty(payload)
                    ? "ℹ️ Конкурс не найден или уже недоступен."
                    : null;

                await _showNeutralStartAsync(botClient, chatId, null, reason, token);
                return;
            }

            // === Finished contests: always show card (no context activation) ===
            if (string.Equals(contest.Status, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                string note = null;
                if (isReferralPayload)
                    note = "ℹ️ Конкурс завершён — переход по реферальной ссылке не засчитан.";

                await _showFinishedContestCardAsync(
                    botClient,
                    msg,
                    token,
                    null,
                    contest.Id,
                    "back_start",
                    note);

                return;
            }

            // === Not running / outside schedule ===
            if (!string.Equals(contest.Status, "Running", StringComparison.OrdinalIgnoreCase))
            {
                await _showNeutralStartAsync(botClient, chatId, null, "ℹ️ Конкурс сейчас недоступен.", token);
                return;
            }

            if (DateTime.Now < contest.StartAt)
            {
                await _showNeutralStartAsync(
                    botClient,
                    chatId,
                    null,
                    $"ℹ️ Конкурс ещё не начался. Старт: {contest.StartAt:dd.MM.yyyy HH:mm}",
                    token);
                return;
            }

            if (DateTime.Now > contest.EndAt)
            {
                await _showNeutralStartAsync(botClient, chatId, null, "ℹ️ Этот розыгрыш уже завершён.", token);
                return;
            }

            // === Channel required for subscription check ===
            if (!contest.ChannelId.HasValue)
            {
                await _showNeutralStartAsync(
                    botClient,
                    chatId,
                    null,
                    "Пост конкурса сейчас недоступен.\nПопроси админа перепубликовать конкурс в канал.",
                    token);
                return;
            }

            // === Subscription check (per contest channel) ===
            bool isSubscribed = await _subs.IsUserSubscribedAsync(botClient, contest.ChannelId.Value, userId, token); 

            if (!isSubscribed)
            {
                string reason = isReferralPayload
                    ? "ℹ️ Ты пришёл по реферальной ссылке, но пока не подписан на канал конкурса.\nПодпишись и открой ссылку ещё раз 🙂"
                    : "ℹ️ Чтобы участвовать, нужно быть подписанным на канал конкурса.\nПодпишись на канал 🙂";

                await _showNeutralStartAsync(botClient, chatId, null, reason, token);
                return;
            }

            // === Register participant ===
            bool registeredNow = _participants.RegisterParticipantForContest(
                 userId,
                 username,
                 contest,
                 msg.From.FirstName,
                 msg.From.LastName
             );

            // контекст активного конкурса задаём только для Running
            _setLastContestId(userId, contest.Id);

            bool referralCounted = false;
            string headerNote = null;

            // ref counting rules:
            // - only referral contests
            // - inviter must already be participant (enforced inside services)
            // - if user already registered -> cannot become referral
            bool isReferralContest = string.Equals(contest.Type, "referral", StringComparison.OrdinalIgnoreCase);

            if (inviterId.HasValue && inviterId.Value != userId && isReferralContest)
            {
                if (!registeredNow)
                {
                    headerNote = "ℹ️ Ты уже участвуешь, поэтому переход по реферальной ссылке не засчитан.";
                }
                else
                {
                    referralCounted = _participants.AddReferral(inviterId.Value, userId, contest);
                    if (!referralCounted)
                        headerNote = "ℹ️ Переход по реферальной ссылке не засчитан.";
                }
            }

            // update channel post only once (if anything changed)
            if (registeredNow || referralCounted)
                await _channelPosts.TryUpdateChannelPostAsync(botClient, contest, token);

            await _showUserMenuAsync(botClient, chatId, userId, contest, null, headerNote, token);
        }

        private static string ExtractPayload(string fullText)
        {
            if (string.IsNullOrWhiteSpace(fullText)) return string.Empty;

            string[] parts = fullText.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? parts[1].Trim() : string.Empty;
        }
    }
}
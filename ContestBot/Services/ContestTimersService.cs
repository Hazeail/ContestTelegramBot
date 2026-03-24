using System;
using System.Net;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ContestBot.Services.Draw;

namespace ContestBot.Services
{
    internal sealed class ContestTimersService
    {
        private readonly ContestManager _contestManager;
        private readonly ContestDrawService _draw;

        private readonly long _adminUserId;
        private readonly Dictionary<int, DateTime> _lastAutoDrawSuccessNotifyAt = new Dictionary<int, DateTime>();
        private readonly OwnerNotificationService _ownerNotify;

        public ContestTimersService(
            ContestManager contestManager,
            ContestDrawService draw,
            long adminUserId,
            Func<long, bool> isAdmin)
        {
            _contestManager = contestManager;
            _draw = draw;
            _adminUserId = adminUserId;

_ownerNotify = new OwnerNotificationService(
   (b, chatId, msg, ct) => b.SendMessage(
        chatId,
        msg,
        parseMode: ParseMode.Html,
        cancellationToken: ct
    ),
    _adminUserId,
    isAdmin
);
        }

        public async Task CheckContestTimersAsync(ITelegramBotClient botClient, CancellationToken token)
        {
            await CheckContestAutoDrawAsync(botClient, token);
        }
        public Task TickAutoDrawAsync(ITelegramBotClient botClient, CancellationToken token)
        {
            return CheckContestAutoDrawAsync(botClient, token);
        }
        private async Task CheckContestAutoDrawAsync(ITelegramBotClient botClient, CancellationToken token)
        {
            var contest = _contestManager.GetCurrentContest();
            if (contest == null) return;

            long ownerId = contest.CreatedByAdminUserId ?? 0;
            if (ownerId <= 0) ownerId = _adminUserId; // если создателя нет — fallback супер-админ

            var now = DateTime.Now;
            var action = AutoDrawTickDecision.Decide(contest, now);

            if (action == AutoDrawTickAction.None)
                return;

            if (action == AutoDrawTickAction.NotifyDrawingStuck)
            {
                // === это 1-в-1 твой старый код из if (contest.Status == "Drawing") ===
                var stuckFor = now - contest.EndAt;

                if (stuckFor >= TimeSpan.FromMinutes(2))
                {
                    DateTime last;
                    _lastAutoDrawSuccessNotifyAt.TryGetValue(contest.Id, out last);

                    if (last == default(DateTime) || (now - last) >= TimeSpan.FromMinutes(10))
                    {
                        _lastAutoDrawSuccessNotifyAt[contest.Id] = now;

                        try
                        {
                            string safeName = WebUtility.HtmlEncode(contest.Name ?? "");

                            await _ownerNotify.SendToOwnerWithFallbackAsync(
                                botClient,
                                ownerId,
                                "<b>⚠️ Конкурс завис в статусе Drawing</b>\n" +
                                "Конкурс: <code>#" + contest.Id + "</code> — <b>" + safeName + "</b>\n" +
                                "Окончание: <code>" + contest.EndAt.ToString("dd.MM.yyyy HH:mm") + "</code>\n\n" +
                                "<b>Что делать:</b> Админка → «Конкурсы» → открой конкурс → «Перевыбрать».",
                                token);
                        }
                        catch { }
                    }
                }

                return;
            }

            // 1) Ставим “замок” ПЕРЕД розыгрышем и сохраняем в БД
            contest.Status = "Drawing";
            Database.SaveContest(contest);

            
            // 2) Теперь можно выбирать победителей (мы разрешили Drawing как “active” выше)
            List<Participant> winners;
            try
            {
                winners = await _draw.DoDrawAsync(botClient, token);
            }
            catch (Exception ex)
            {
                // Если что-то пошло совсем не так — повторно не розыгрываем (статус уже Drawing)
                try
                {

                    string safeName = WebUtility.HtmlEncode(contest.Name ?? "");
                    string safeErr = WebUtility.HtmlEncode(ex.Message ?? "");
                    
                    await _ownerNotify.SendToOwnerWithFallbackAsync(
                        botClient, 
                        ownerId,
                        "<b>⚠️ Автодроу завершился ошибкой</b>\n" +
                        "Конкурс: <code>#" + contest.Id + "</code> — <b>" + safeName + "</b>\n" +
                        "Статус: <code>Drawing</code> (защита от повторного розыгрыша)\n\n" +
                        "Причина: <i>" + safeErr + "</i>\n\n" +
                        "<b>Действие:</b> Админка → «Конкурсы» → открой конкурс → «Перевыбрать».",
                        token);
                }
                catch { }

                return;
            }

            var sb = new System.Text.StringBuilder();
            string safeNameSB = WebUtility.HtmlEncode(contest.Name ?? "");

            sb.AppendLine("<b>✅ Автоматический розыгрыш завершён</b>");
            sb.AppendLine("Конкурс: <code>#" + contest.Id + "</code> — <b>" + safeNameSB + "</b>");
            sb.AppendLine();
            sb.AppendLine("<b>Победители:</b>");

            for (int i = 0; i < winners.Count; i++)
            {
                var w = winners[i];

                string display = w.GetDisplayNameWithoutId();

                string winnerHtml;
                if (!string.IsNullOrWhiteSpace(display) && display.StartsWith("@"))
                {
                    // без <code>
                    winnerHtml = WebUtility.HtmlEncode(display);
                }
                else if (!string.IsNullOrWhiteSpace(display))
                {
                    winnerHtml = "<a href=\"tg://user?id=" + w.UserId + "\">" + WebUtility.HtmlEncode(display) + "</a>";
                }
                else
                {
                    winnerHtml = "<a href=\"tg://user?id=" + w.UserId + "\">Открыть профиль</a>";
                }

                sb.AppendLine((i + 1) + ") " + winnerHtml);
            }

            // анти-спам: если по каким-то причинам успех-уведомление пытаются отправить повторно
            if (_lastAutoDrawSuccessNotifyAt.TryGetValue(contest.Id, out var lastOk) &&
                (now - lastOk).TotalMinutes < 10)
            {
                return;
            }

            try
            {
                await _ownerNotify.SendToOwnerWithFallbackAsync(botClient, ownerId, sb.ToString(), token);
                _lastAutoDrawSuccessNotifyAt[contest.Id] = now;
            }
            catch { }
        }
        internal Task CheckContestAutoDrawAsync_ForTests(ITelegramBotClient botClient, CancellationToken token)
        {
            return CheckContestAutoDrawAsync(botClient, token);
        }
    }
}

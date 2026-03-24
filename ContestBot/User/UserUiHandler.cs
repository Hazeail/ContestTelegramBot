using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot.User
{
    internal sealed class UserUiHandler
    {
        private readonly Func<long, int?> _getLastContestIdForUser;
        private readonly Func<int, Contest> _getContestById;

        private readonly Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> _sendOrEditHtmlAsync;

        public UserUiHandler(
            Func<long, int?> getLastContestIdForUser,
            Func<int, Contest> getContestById,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync)
        {
            _getLastContestIdForUser = getLastContestIdForUser;
            _getContestById = getContestById;
            _sendOrEditHtmlAsync = sendOrEditHtmlAsync;
        }

        public async Task ShowNeutralStartAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? editMessageId,
            string headerText,
            CancellationToken token)
        {
            var kbNeutral = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Сменить конкурс", "contests:menu:back_start") }
            });

            string prefix = string.IsNullOrWhiteSpace(headerText)
                ? string.Empty
                : EscapeHtml(headerText.Trim()) + "\n\n";

            string neutralText =
                prefix +
                "<b>👋 Привет!</b>\n\n" +
                "Чтобы участвовать в конкурсе, открой пост конкурса в канале и нажми кнопку «Участвовать».\n\n" +
                "Ссылки на посты можно найти во вкладке «Все активные».";

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, neutralText, kbNeutral, token);
        }

        public async Task ShowUserMenuAsync(
            ITelegramBotClient botClient,
            long chatId,
            long userId,
            Contest contest,
            int? editMessageId,
            string headerText,
            CancellationToken token)
        {
            if (contest == null)
            {
                var activeId = _getLastContestIdForUser(userId);
                contest = activeId.HasValue ? _getContestById(activeId.Value) : null;
            }

            string prefix = string.IsNullOrWhiteSpace(headerText)
               ? string.Empty
               : EscapeHtml(headerText.Trim()) + "\n\n";

            // ЭКРАН 9 — нейтральный старт (если контекста нет)
            if (contest == null)
            {
                var kbNeutral = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Сменить конкурс", "contests:menu:back_start") }
                });

                string neutralText =
                    prefix + "<b>👋 Привет!</b>\n\n" +
                    "Чтобы участвовать в конкурсе, открой пост конкурса в канале и нажми кнопку «Участвовать».\n\n" +
                    "Ссылки на посты можно найти во вкладке «Все активные».";

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, neutralText, kbNeutral, token);
                return;
            }

            bool isReferral = string.Equals(contest.Type, "referral", StringComparison.OrdinalIgnoreCase);

            string statusText =
                string.Equals(contest.Status, "Finished", StringComparison.OrdinalIgnoreCase) ? "завершён" :
                string.Equals(contest.Status, "Running", StringComparison.OrdinalIgnoreCase) ? "активен" :
                "не активен";

            string endAtText = contest.EndAt.ToString("dd.MM.yyyy HH:mm");

            // ЭКРАН 1B — WELCOME (реф-конкурс)
            if (isReferral)
            {
                string text =
                    prefix + "<b>Круто! Ты участвуешь ✅</b>\n\n" +
                    $"Реф-конкурс: {EscapeHtml(contest.Name)}\n" +
                    $"Статус: {EscapeHtml(statusText)} • до {EscapeHtml(endAtText)}\n\n" +
                    "Ссылка и список рефералов — в кнопках ниже.";

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("📎 Моя ссылка", "open_ref"),
                        InlineKeyboardButton.WithCallbackData("👥 Мои рефералы", "open_myref")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Сменить конкурс", "contests:menu:back_menu")
                    }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, kb, token);
                return;
            }

            // ЭКРАН 1A — WELCOME (обычный конкурс)
            {
                string text =
                    prefix + "<b>Готово! Ты в игре ✅</b>\n\n" +
                    $"Конкурс: {EscapeHtml(contest.Name)}\n" +
                    $"Статус: {EscapeHtml(statusText)} • до {EscapeHtml(endAtText)}\n\n" +
                    "Удачи! Результаты появятся после завершения конкурса.";

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Сменить конкурс", "contests:menu:back_menu") }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, kb, token);
            }
        }

        private static string EscapeHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}

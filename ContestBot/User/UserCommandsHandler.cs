using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot.User
{
    internal sealed class UserCommandsHandler
    {
        private readonly ContestManager _contestManager;
        private readonly ParticipantsManager _participantsManager;

        private readonly Func<long, int?> _getLastContestIdForUser;
        private readonly Action<long, int> _setLastContestIdForUser;

        private readonly Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> _sendOrEditHtmlAsync;
        private readonly string _botUsername;

        public UserCommandsHandler(
            ContestManager contestManager,
            ParticipantsManager participantsManager,
            Func<long, int?> getLastContestIdForUser,
            Action<long, int> setLastContestIdForUser,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
            string botUsername)
        {
            _contestManager = contestManager;
            _participantsManager = participantsManager;
            _getLastContestIdForUser = getLastContestIdForUser;
            _setLastContestIdForUser = setLastContestIdForUser;
            _sendOrEditHtmlAsync = sendOrEditHtmlAsync;
            _botUsername = botUsername;
        }

        public async Task HandleRefCommandAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId = null)
        {
            long chatId = msg.Chat.Id;
            long userId = msg.From.Id;
            string username = msg.From.Username;

            var lastContestId = _getLastContestIdForUser(userId);
            if (!lastContestId.HasValue)
            {
                await _sendOrEditHtmlAsync(
                    botClient, chatId, editMessageId,
                    "Сначала зайди в конкурс через кнопку «Участвовать» под постом в канале — тогда я пойму, для какого конкурса нужно сгенерировать ссылку 🙂",
                    null, token);
                return;
            }

            var contest = _contestManager.GetContestById(lastContestId.Value);
            if (contest == null)
            {
                await _sendOrEditHtmlAsync(
                    botClient, chatId, editMessageId,
                    "Текущий конкурс не найден. Попробуй снова зайти через кнопку «Участвовать» в канале.",
                    null, token);
                return;
            }

            if (!string.Equals(contest.Type, "referral", StringComparison.OrdinalIgnoreCase))
            {
                var kbNoRef = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back_menu"),
                        InlineKeyboardButton.WithCallbackData("🔁 Сменить конкурс", "contests:menu:back_menu")
                    }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, "В этом конкурсе нет реферальной системы 🙂", kbNoRef, token);
                return;
            }

            _participantsManager.RegisterParticipantForContest(userId, username, contest);

            string payload = $"ref_{contest.Code}_{userId}";
            string link = $"https://t.me/{_botUsername}?start={payload}";

            var kb = new InlineKeyboardMarkup(new[]
             {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back_menu"),
                    InlineKeyboardButton.WithCallbackData("👥 Мои рефералы", "open_myref")
                }
            });

            string html =
                "<b>📎 Моя ссылка</b>\n\n" +
                "Отправь её другу — когда он зайдёт по ней, он автоматически станет твоим рефералом в этом конкурсе.\n\n" +
                "Твоя ссылка:\n" +
                EscapeHtml(link) + "\n\n" +
                "Если друг уже участвует — он не засчитается повторно.";

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, html, kb, token);
        }

        public async Task HandleMyRefCommandAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId = null)
        {
            long chatId = msg.Chat.Id;
            long userId = msg.From.Id;

            var lastContestId = _getLastContestIdForUser(userId);
            if (!lastContestId.HasValue)
            {
                await _sendOrEditHtmlAsync(
                    botClient, chatId, editMessageId,
                    "Сначала зайди в конкурс через кнопку «Участвовать» в канале, чтобы я понял, для какого конкурса показать рефералов 🙂",
                    null, token);
                return;
            }

            var contest = _contestManager.GetContestById(lastContestId.Value);
            if (contest == null)
            {
                await _sendOrEditHtmlAsync(
                    botClient, chatId, editMessageId,
                    "Текущий конкурс не найден. Попробуй снова зайти через кнопку «Участвовать» в канале.",
                    null, token);
                return;
            }

            if (!string.Equals(contest.Type, "referral", StringComparison.OrdinalIgnoreCase))
            {
                var kbNoRef = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back_menu"),
                        InlineKeyboardButton.WithCallbackData("🔁 Сменить конкурс", "contests:menu:back_menu")
                    }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, "В этом конкурсе нет реферальной системы 🙂", kbNoRef, token);
                return;
            }

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back_menu"),
                    InlineKeyboardButton.WithCallbackData("📎 Моя ссылка", "open_ref")
                }
            });

            var participant = _participantsManager.GetParticipant(userId, contest.Id);
            if (participant == null)
            {
                await _sendOrEditHtmlAsync(
                    botClient, chatId, editMessageId,
                    "Ты ещё не участвуешь в этом конкурсе.\nПерейди в пост конкурса в канале и нажми кнопку «Участвовать» 🙂",
                    kb, token);
                return;
            }

            var referrals = _participantsManager.GetReferrals(userId, contest.Id);
            if (referrals == null || referrals.Count == 0)
            {
                string htmlEmpty =
                    "<b>👥 Мои рефералы</b>\n\n" +
                    "Всего: 0\n\n" +
                    "Пока никто не присоединился по твоей ссылке. Отправь её другу — и он появится здесь.";

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, htmlEmpty, kb, token);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<b>👥 Мои рефералы</b>");
            sb.AppendLine();
            sb.AppendLine($"Всего: {referrals.Count}");
            sb.AppendLine();

            foreach (var r in referrals)
            {
                if (!string.IsNullOrWhiteSpace(r.Username))
                    sb.AppendLine("• @" + EscapeHtml(r.Username.Trim()));
                else
                    sb.AppendLine("• id:" + r.UserId);
            }

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, sb.ToString(), kb, token);
        }

        public async Task HandleContestsMenuScreenAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId = null,
            string origin = "back_menu")
        {
            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👤 Мои конкурсы", $"contests:mine:{origin}"),
                    InlineKeyboardButton.WithCallbackData("🔥 Все активные", $"contests:active:{origin}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Назад", origin)
                }
            });

            string text =
                "<b>🔁 Сменить конкурс</b>\n\n" +
                "Выбери раздел:\n\n" +
                "• Мои конкурсы — только те, где ты уже участвуешь\n" +
                "• Все активные — список конкурсов и ссылка на пост в канале\n" +
                "Вступить можно только через пост в канале, где опубликован конкурс.";

            await _sendOrEditHtmlAsync(botClient, msg.Chat.Id, editMessageId, text, kb, token);
        }

        public async Task HandleMyContestsScreenAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId = null,
            string origin = "back_menu")
        {
            long chatId = msg.Chat.Id;
            long userId = msg.From.Id;

            var contestIds = Database.GetContestIdsForUser(userId);
            if (contestIds == null || contestIds.Count == 0)
            {
                var kb0 = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"contests:menu:{origin}") }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId,
                    "<b>👤 Мои конкурсы</b>\n\n" +
                    "Пока нет конкурсов, где ты участвуешь.\n\n" +
                    "Чтобы вступить — нажми «Участвовать» в посте конкурса в канале.\n" +
                    "Ссылку на пост можно найти во вкладке «Все активные».",
                    kb0, token);
                return;
            }

            var all = Database.LoadAllContests();
            var my = all.Where(c => contestIds.Contains(c.Id)).OrderBy(c => c.EndAt).ToList();

            if (my.Count == 0)
            {
                var kb0 = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"contests:menu:{origin}") }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId,
                    "Не нашёл конкурсы, где ты участвуешь (возможно, данные обновились).\nПопробуй снова нажать «Участвовать» в канале.",
                    kb0, token);
                return;
            }

            var rows = new List<InlineKeyboardButton[]>();

            foreach (var c in my)
            {
                string mark = string.Equals(c.Status, "Finished", StringComparison.OrdinalIgnoreCase) ? "🏁 " : "✅ ";

                bool hasContext = _getLastContestIdForUser(userId).HasValue;
                string back = hasContext ? "back_menu" : "back_start";

                string callback =
                    string.Equals(c.Status, "Finished", StringComparison.OrdinalIgnoreCase)
                        ? $"finished_card:{c.Id}:{back}"
                        : $"switch_contest:{c.Id}:{origin}";

                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{mark}{c.Name}", callback)
                });
            }

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("Назад", $"contests:menu:{origin}")
            });

            var kb = new InlineKeyboardMarkup(rows);

            string text =
                "<b>👤 Мои конкурсы</b>\n\n" +
                "Здесь только те конкурсы, где ты уже участвуешь.\n\n" +
                "Статусы:\n" +
                "✅ — активен\n" +
                "🏁 — завершён";

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, kb, token);
        }

        public async Task HandleActiveContestsScreenAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId = null,
            string origin = "back_menu")
        {
            long chatId = msg.Chat.Id;

            var all = Database.LoadAllContests();
            var active = all.Where(c => string.Equals(c.Status, "Running", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(c => c.EndAt)
                            .ToList();

            if (active.Count == 0)
            {
                var kb0 = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"contests:menu:{origin}") }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId,
                    "<b>🔥 Все активные</b>\n\n" +
                    "Сейчас нет активных конкурсов.\n\n" +
                    "Когда появится новый розыгрыш, он будет опубликован в канале.\n" +
                    "Чтобы вступить — нажми «Участвовать» в посте конкурса.", kb0, token);
                return;
            }

            long userId = msg.From.Id;
            bool hasContext = _getLastContestIdForUser(userId).HasValue;
            string cardBack = hasContext ? "back_menu" : "back_start";

            var rows = new List<InlineKeyboardButton[]>();

            foreach (var c in active)
            {
                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"{c.Name}",
                        $"contest_card:{c.Id}:{cardBack}"
                    )
                });
            }

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("Назад", $"contests:menu:{origin}")
            });

            var kb = new InlineKeyboardMarkup(rows);

            string text =
                "<b>🔥 Все активные</b>\n\n" +
                "Здесь список текущих конкурсов и ссылка на пост в канале.\n" +
                "Вступить можно только через пост в канале, где опубликован конкурс.\n\n" +
                "Выбери конкурс — покажу карточку и ссылку на пост.";

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, text, kb, token);
        }

        public async Task HandleActiveContestCardScreenAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId,
            int contestId,
            string back = "back_menu")
        {
            long chatId = msg.Chat.Id;

            var contest = Database.LoadAllContests().FirstOrDefault(c => c.Id == contestId);
            if (contest == null)
            {
                var kbNF = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Назад", back) }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, "Конкурс не найден.", kbNF, token);
                return;
            }

            string typeText = string.Equals(contest.Type, "referral", StringComparison.OrdinalIgnoreCase)
                ? "реф-конкурс"
                : "обычный";

            string statusText =
                string.Equals(contest.Status, "Running", StringComparison.OrdinalIgnoreCase) ? "активен" :
                string.Equals(contest.Status, "Finished", StringComparison.OrdinalIgnoreCase) ? "завершён" :
                "не активен";

            string endAtText = contest.EndAt.ToString("dd.MM.yyyy HH:mm");

            string shortInfo = (contest.Description ?? "").Trim();
            if (shortInfo.Length > 240) shortInfo = shortInfo.Substring(0, 240).TrimEnd() + "…";

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Назад", back) }
            });

            // ссылка на пост
            string username = (contest.ChannelUsername ?? "").Trim().TrimStart('@');
            bool hasLink = contest.ChannelPostMessageId.HasValue && !string.IsNullOrEmpty(username);

            if (!hasLink)
            {
                // Экран 5B
                string textB =
                    $"<b>📌 {EscapeHtml(contest.Name)}</b>\n\n" +
                    $"Тип: {EscapeHtml(typeText)}\n" +
                    $"Статус: {EscapeHtml(statusText)} • до {EscapeHtml(endAtText)}\n\n" +
                    $"{EscapeHtml(shortInfo)}\n\n" +
                    "Пост конкурса сейчас недоступен.\n" +
                    "Попроси админа перепубликовать конкурс в канал.";

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, textB, kb, token);
                return;
            }

            // Экран 5A — с HTML-ссылкой внутри текста
            string url = $"https://t.me/{username}/{contest.ChannelPostMessageId.Value}";
            string linkHtml = $"<a href=\"{url}\">открыть</a>";

            string textA =
                $"<b>📌 {EscapeHtml(contest.Name)}</b>\n\n" +
                $"Тип: {EscapeHtml(typeText)}\n" +
                $"Статус: {EscapeHtml(statusText)} • до {EscapeHtml(endAtText)}\n\n" +
                $"{EscapeHtml(shortInfo)}\n\n" +
                $"Смотреть пост в канале — {linkHtml}.";

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, textA, kb, token);
        }

        public async Task HandleFinishedContestCardScreenAsync(
            ITelegramBotClient botClient,
            Message msg,
            CancellationToken token,
            int? editMessageId,
            int contestId,
            string back = "back_menu",
            string topNoteText = null)
        {
            long chatId = msg.Chat.Id;

            var contest = Database.LoadAllContests().FirstOrDefault(c => c.Id == contestId);
            if (contest == null)
            {
                var kbNF = new InlineKeyboardMarkup(new[]
                {
                     new[]
                     {
                         InlineKeyboardButton.WithCallbackData("⬅️ Назад", back),
                         InlineKeyboardButton.WithCallbackData("Сменить конкурс", $"contests:menu:{back}")
                     }
                });

                await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, "Конкурс не найден.", kbNF, token);
                return;
            }

            string endAtText = contest.EndAt.ToString("dd.MM.yyyy HH:mm");

            var winners = Database.LoadWinnersForContest(contest);

            // ссылка на пост
            string username = (contest.ChannelUsername ?? "").Trim().TrimStart('@');
            bool hasLink = contest.ChannelPostMessageId.HasValue && !string.IsNullOrEmpty(username);

            string linkBlock;
            if (hasLink)
            {
                string url = $"https://t.me/{username}/{contest.ChannelPostMessageId.Value}";
                linkBlock = $"Смотреть пост в канале — <a href=\"{url}\">открыть</a>.";
            }
            else
            {
                linkBlock =
                    "Пост конкурса сейчас недоступен.\n" +
                    "Попроси админа перепубликовать конкурс в канал.";
            }

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", back),
                    InlineKeyboardButton.WithCallbackData("Сменить конкурс", $"contests:menu:{back}")
                }
            });

            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrWhiteSpace(topNoteText))
            {
                sb.AppendLine(EscapeHtml(topNoteText));
                sb.AppendLine();
            }

            sb.AppendLine("<b>🏁 Конкурс завершён</b>");
            sb.AppendLine();
            sb.AppendLine($"Конкурс: {EscapeHtml(contest.Name)}");
            sb.AppendLine($"Дата завершения: {EscapeHtml(endAtText)}");
            sb.AppendLine();

            if (winners != null && winners.Count > 0)
            {
                sb.AppendLine("Победители:");
                int pos = 1;
                foreach (var w in winners)
                {
                    string display = w.GetDisplayNameWithoutId();

                    if (!string.IsNullOrWhiteSpace(display) && display.StartsWith("@"))
                    {
                        // без <code>
                        sb.AppendLine("• " + EscapeHtml(display));
                    }
                    else if (!string.IsNullOrWhiteSpace(display))
                    {
                        // имя ссылкой
                        sb.AppendLine("• <a href=\"tg://user?id=" + w.UserId + "\">" + EscapeHtml(display) + "</a>");
                    }
                    else
                    {
                        sb.AppendLine("• <a href=\"tg://user?id=" + w.UserId + "\">Открыть профиль</a>");
                    }

                    pos++;
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Результаты пока не опубликованы.");
                sb.AppendLine();
            }

            // linkBlock может содержать HTML (<a>), поэтому экранируем только если там нет ссылки
            if (hasLink)
                sb.AppendLine(linkBlock);
            else
                sb.AppendLine(EscapeHtml(linkBlock));

            await _sendOrEditHtmlAsync(botClient, chatId, editMessageId, sb.ToString(), kb, token);
        }

        private static string EscapeHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

    }
}

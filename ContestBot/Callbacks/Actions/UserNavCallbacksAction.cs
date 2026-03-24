using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Utils;

namespace ContestBot.Callbacks.Actions
{
    internal sealed class UserNavCallbacksAction : ICallbackAction
    {
        private readonly Action<long, int> _setLastContestIdForUser;

        private readonly Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> _sendOrEditHtmlAsync;

        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, Task> _handleRefAsync;
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, Task> _handleMyRefAsync;
        private readonly Func<ITelegramBotClient, long, long, int?, CancellationToken, Task> _showUserMenuAsync;

        public UserNavCallbacksAction(
            Action<long, int> setLastContestIdForUser,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
            Func<ITelegramBotClient, Message, CancellationToken, int?, Task> handleRefAsync,
            Func<ITelegramBotClient, Message, CancellationToken, int?, Task> handleMyRefAsync,
            Func<ITelegramBotClient, long, long, int?, CancellationToken, Task> showUserMenuAsync)
        {
            _setLastContestIdForUser = setLastContestIdForUser;

            _sendOrEditHtmlAsync = sendOrEditHtmlAsync;

            _handleRefAsync = handleRefAsync;
            _handleMyRefAsync = handleMyRefAsync;
            _showUserMenuAsync = showUserMenuAsync;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null) return false;

            long userId = cq.From.Id;
            long chatId = cq.Message?.Chat.Id ?? userId;
            int? editMessageId = cq.Message?.MessageId;

            // back_menu
            if (cq.Data == "back_menu")
            {
                try { await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct); } catch { }
                await _showUserMenuAsync(bot, chatId, userId, editMessageId, ct);
                return true;
            }

            // back_start (нейтральный старт)
            if (cq.Data == "back_start")
            {
                try { await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct); } catch { }

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Сменить конкурс", "contests:menu:back_start") }
                });

                string text =
                    "<b>👋 Привет!</b>\n\n" +
                    "Чтобы участвовать в конкурсе, открой пост конкурса в канале и нажми кнопку «Участвовать».\n\n" +
                    "Ссылки на посты можно найти во вкладке «Все активные».";

                await _sendOrEditHtmlAsync(bot, chatId, editMessageId, text, kb, ct);
                return true;
            }

            // open_ref
            if (cq.Data == "open_ref")
            {
                var fake = new Message { Chat = new Chat { Id = chatId }, From = cq.From };
                try { await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct); } catch { }
                await _handleRefAsync(bot, fake, ct, editMessageId);
                return true;
            }

            // open_myref
            if (cq.Data == "open_myref")
            {
                var fake = new Message { Chat = new Chat { Id = chatId }, From = cq.From };
                try { await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct); } catch { }
                await _handleMyRefAsync(bot, fake, ct, editMessageId);
                return true;
            }

            // switch_contest:{contestId}:{origin}
            if (cq.Data.StartsWith("switch_contest:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int contestId))
                {
                    string origin = (parts.Length >= 3) ? parts[2] : "back_menu";

                    _setLastContestIdForUser(userId, contestId);
                    try { await bot.AnswerCallbackQuery(cq.Id, "Контекст конкурса переключён ✅", cancellationToken: ct); } catch { }

                    var fake = new Message { Chat = new Chat { Id = chatId }, From = cq.From };

                    // back_menu -> показываем нормальное меню (оно само прячет ref-кнопки, если конкурс не referral)
                    await _showUserMenuAsync(bot, chatId, userId, editMessageId, ct);
                    return true;
                }

                try { await bot.AnswerCallbackQuery(cq.Id, "Некорректный contestId.", cancellationToken: ct); } catch { }
                return true;
            }

            return false;
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Callbacks.Actions
{
    internal sealed class UserFinishedContestCardCallbacksAction : ICallbackAction
    {
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, string, Task> _showFinishedCard;

        public UserFinishedContestCardCallbacksAction(
            Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, string, Task> showFinishedCard)
        {
            _showFinishedCard = showFinishedCard ?? throw new ArgumentNullException(nameof(showFinishedCard));
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null) return false;

            if (!cq.Data.StartsWith("finished_card:", StringComparison.OrdinalIgnoreCase))
                return false;

            long chatId = cq.Message?.Chat.Id ?? cq.From.Id;
            int? editMessageId = cq.Message?.MessageId;

            var parts = cq.Data.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[1], out int contestId))
            {
                try { await bot.AnswerCallbackQuery(cq.Id, "Некорректный contestId.", cancellationToken: ct); } catch { }
                return true;
            }

            string back = (parts.Length >= 3 ? parts[2] : "back_menu")?.Trim().ToLowerInvariant();
            if (back != "back_menu" && back != "back_start")
                back = "back_menu";

            try { await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct); } catch { }

            // Нам нужен Message для сигнатуры (chat/from/edit id)
            var fake = new Message { Chat = new Chat { Id = chatId }, From = cq.From };
            await _showFinishedCard(bot, fake, ct, editMessageId, contestId, back, null);
            return true;
        }
    }
}
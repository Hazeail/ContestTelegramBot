using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Callbacks.Actions
{
    internal sealed class UserContestCardCallbacksAction : ICallbackAction
    {
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, Task> _showCard;

        public UserContestCardCallbacksAction(
            Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, Task> showCard)
        {
            _showCard = showCard ?? throw new ArgumentNullException(nameof(showCard));
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null) return false;

            if (!cq.Data.StartsWith("contest_card:", StringComparison.OrdinalIgnoreCase))
                return false;

            long userId = cq.From.Id;
            long chatId = cq.Message?.Chat.Id ?? userId;
            int? editMessageId = cq.Message?.MessageId;

            var parts = cq.Data.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[1], out int contestId))
            {
                try { await bot.AnswerCallbackQuery(cq.Id, "Некорректный contestId.", cancellationToken: ct); } catch { }
                return true;
            }

            string back = (parts.Length >= 3) ? (parts[2] ?? "back_menu").Trim().ToLowerInvariant() : "back_menu";
            if (back != "back_menu" && back != "back_start")
                back = "back_menu";

            try { await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct); } catch { }

            var fake = new Message { Chat = new Chat { Id = chatId }, From = cq.From };
            await _showCard(bot, fake, ct, editMessageId, contestId, back);
            return true;
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Utils;

namespace ContestBot.Callbacks.Actions
{
    internal sealed class AdminDrawCallbacksAction : ICallbackAction
    {
        private readonly Func<long, bool> _isAdmin;
        private readonly Func<ITelegramBotClient, long, int, int?, CancellationToken, Task> _runDrawAsync;


        public AdminDrawCallbacksAction(
            Func<long, bool> isAdmin,
            Func<ITelegramBotClient, long, int, int?, CancellationToken, Task> runDrawAsync)
        {
            _isAdmin = isAdmin;
            _runDrawAsync = runDrawAsync;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null) return false;

            bool isMine = CallbackDataParser.TryParseAdminDraw(cq.Data, out _);

            if (!isMine) return false;

            long userId = cq.From.Id;
            long chatId = cq.Message?.Chat.Id ?? userId;
            int? editMessageId = cq.Message?.MessageId;

            if (!_isAdmin(userId))
            {
                await SafeAnswer(bot, cq.Id, "Недостаточно прав.", ct);
                return true;
            }

            await SafeAnswer(bot, cq.Id, null, ct);

            if (CallbackDataParser.TryParseAdminDraw(cq.Data, out int contestId))
            {
                await _runDrawAsync(bot, chatId, contestId, editMessageId, ct);
                return true;
            }

            return true;
        }

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
    }
}

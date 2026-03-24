using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Callbacks
{
    internal sealed class CallbackUpdateHandler
    {
        private readonly CallbackRouter _router;

        public CallbackUpdateHandler(CallbackRouter router)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public async Task HandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null)
            {
                try { await bot.AnswerCallbackQuery(cq?.Id, cancellationToken: ct); } catch { }
                return;
            }

            bool handled = await _router.TryHandleAsync(bot, cq, ct);

            // Раньше тут был legacy fallback.
            // Теперь: если никто не обработал callback — просто "гасим" нажатие, без побочных эффектов.
            if (!handled)
            {
                try { await bot.AnswerCallbackQuery(cq.Id, cacheTime: 0, cancellationToken: ct); } catch { }
            }
        }
    }
}
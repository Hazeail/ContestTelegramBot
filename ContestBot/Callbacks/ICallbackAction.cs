using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Callbacks
{
    internal interface ICallbackAction
    {
        Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct);
    }
}
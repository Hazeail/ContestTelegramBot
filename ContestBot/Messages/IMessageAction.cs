using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Messages
{
    internal interface IMessageAction
    {
        Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct);
    }
}

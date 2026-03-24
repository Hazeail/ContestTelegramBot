using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Utils;

namespace ContestBot.Callbacks.Actions
{
    internal sealed class UserContestsScreensAction : ICallbackAction
    {
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> _menu;
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> _mine;
        private readonly Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> _active;

        public UserContestsScreensAction(
            Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> handleContestsMenuScreenAsync,
            Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> handleMyContestsScreenAsync,
            Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> handleActiveContestsScreenAsync)
        {
            _menu = handleContestsMenuScreenAsync ?? throw new ArgumentNullException(nameof(handleContestsMenuScreenAsync));
            _mine = handleMyContestsScreenAsync ?? throw new ArgumentNullException(nameof(handleMyContestsScreenAsync));
            _active = handleActiveContestsScreenAsync ?? throw new ArgumentNullException(nameof(handleActiveContestsScreenAsync));
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null) return false;

            long userId = cq.From.Id;
            long chatId = cq.Message?.Chat.Id ?? userId;
            int? editMessageId = cq.Message?.MessageId;

            // новые экраны: contests:menu|mine|active:{origin}
            if (CallbackDataParser.TryParseContestsScreen(cq.Data, out var screen, out var origin2))
            {
                await SafeAnswer(bot, cq.Id, ct);

                var fake = new Message { Chat = new Chat { Id = chatId }, From = cq.From };

                if (screen == "menu")
                {
                    await _menu(bot, fake, ct, editMessageId, origin2);
                    return true;
                }
                if (screen == "mine")
                {
                    await _mine(bot, fake, ct, editMessageId, origin2);
                    return true;
                }
                if (screen == "active")
                {
                    await _active(bot, fake, ct, editMessageId, origin2);
                    return true;
                }

                return true;
            }

            return false;
        }

        private static async Task SafeAnswer(ITelegramBotClient bot, string callbackId, CancellationToken ct)
        {
            try { await bot.AnswerCallbackQuery(callbackId, cancellationToken: ct); } catch { }
        }
    }
}
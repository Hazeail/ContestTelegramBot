using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Services;

namespace ContestBot.Callbacks
{
    internal sealed class CallbackRouter
    {
        private static readonly CrashLogger Log = new CrashLogger();

        private readonly List<ICallbackAction> _actions = new List<ICallbackAction>();

        public CallbackRouter Add(ICallbackAction action)
        {
            _actions.Add(action);
            return this;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];

                try
                {
                    if (await action.TryHandleAsync(bot, cq, ct))
                        return true;
                }
                catch (Exception ex)
                {
                    var chatId = cq?.Message?.Chat?.Id;
                    var fromId = cq?.From?.Id;
                    var data = cq?.Data;

                    Log.Error(
                        $"CallbackAction CRASH: {action.GetType().Name} chatId={chatId} fromId={fromId} data=\"{data}\"",
                        ex
                    );

                    // продолжаем: пусть другие action попробуют обработать
                }
            }

            return false;
        }
    }
}

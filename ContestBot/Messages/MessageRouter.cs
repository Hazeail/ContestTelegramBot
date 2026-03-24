using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Services;

namespace ContestBot.Messages
{
    internal sealed class MessageRouter
    {
        private static readonly CrashLogger Log = new CrashLogger();

        private readonly List<IMessageAction> _actions = new List<IMessageAction>();

        public MessageRouter Add(IMessageAction action)
        {
            _actions.Add(action);
            return this;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];

                try
                {
                    if (await action.TryHandleAsync(bot, msg, text, ct))
                        return true;
                }
                catch (Exception ex)
                {
                    var chatId = msg?.Chat?.Id;
                    var fromId = msg?.From?.Id;

                    Log.Error(
                        $"MessageAction CRASH: {action.GetType().Name} chatId={chatId} fromId={fromId} text=\"{text}\"",
                        ex
                    );

                    // продолжаем цикл, чтобы бот не “умирал” из-за одного action
                }
            }

            return false;
        }
    }
}

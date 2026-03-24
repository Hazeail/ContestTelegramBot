using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ContestBot
{
    internal sealed partial class UpdateHandler
    {
        // TIMERS (вызывается из BotService)
        internal string GetTimerDebugState()
        {
            try
            {
                var c = _root.ContestManager.GetCurrentContest();
                if (c == null) return "CurrentContest=null";

                return
                    $"CurrentContestId={c.Id} " +
                    $"Status={c.Status} " +
                    $"StartAt={c.StartAt:dd.MM.yyyy HH:mm} " +
                    $"EndAt={c.EndAt:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                return "CurrentContest=? error=" + ex.Message;
            }
        }

        public Task CheckContestTimersAsync(ITelegramBotClient botClient, CancellationToken token)
        {
            // Таймер крутится в BotService, но автодроу включаем только флагом
            if (!_root.Settings.AutoDrawEnabled)
                return Task.CompletedTask;

            // Только автодроу. Автозапуск конкурса тут не трогаем.
            return _timers.TickAutoDrawAsync(botClient, token);
        }
    }
}
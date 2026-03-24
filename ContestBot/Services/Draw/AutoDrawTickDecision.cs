using System;

namespace ContestBot.Services.Draw
{
    internal enum AutoDrawTickAction
    {
        None = 0,
        DoAutoDraw,
        NotifyDrawingStuck
    }

    internal static class AutoDrawTickDecision
    {
        public static AutoDrawTickAction Decide(Contest contest, DateTime now)
        {
            if (contest == null) return AutoDrawTickAction.None;

            // Если завис в Drawing — решаем "уведомлять" начиная с +2 минут после EndAt
            if (string.Equals(contest.Status, "Drawing", StringComparison.OrdinalIgnoreCase))
            {
                return (now - contest.EndAt) >= TimeSpan.FromMinutes(2)
                    ? AutoDrawTickAction.NotifyDrawingStuck
                    : AutoDrawTickAction.None;
            }

            // Автодроу — как и сейчас, через твоё правило DrawRules
            if (DrawRules.TryGetAutoDrawMode(contest, now, out _))
                return AutoDrawTickAction.DoAutoDraw;

            return AutoDrawTickAction.None;
        }
    }
}

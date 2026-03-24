using System;

namespace ContestBot.Services.Draw
{
    internal enum DrawMode
    {
        None = 0,
        AutoDraw,
        AdminLateDraw,
        AdminRedraw
    }

    internal static class DrawRules
    {
        public static bool TryGetAutoDrawMode(Contest c, DateTime now, out DrawMode mode)
        {
            mode = DrawMode.None;
            if (c == null) return false;

            if (c.Status != "Running") return false;
            if (now < c.EndAt) return false;

            mode = DrawMode.AutoDraw;
            return true;
        }

        public static bool TryGetAdminDrawMode(Contest c, DateTime now, out DrawMode mode)
        {
            mode = DrawMode.None;
            if (c == null) return false;

            // Админ может только перевыбрать победителей для завершённого конкурса.
            if (c.Status == "Finished")
            {
                mode = DrawMode.AdminRedraw;
                return true;
            }

            return false;
        }
    }
}
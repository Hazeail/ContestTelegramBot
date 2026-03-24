using System;

namespace ContestBot.Utils
{
    internal enum WinnersActionKind
    {
        Inc,
        Dec,
        Ok,
        Noop
    }

    internal static class CallbackDataParser
    {
        internal static bool TryParseWinners(string data, out WinnersActionKind action)
        {
            action = default(WinnersActionKind);
            if (string.IsNullOrWhiteSpace(data)) return false;

            var s = data.Trim();

            if (string.Equals(s, "admin:winners:inc", StringComparison.OrdinalIgnoreCase))
            {
                action = WinnersActionKind.Inc;
                return true;
            }

            if (string.Equals(s, "admin:winners:dec", StringComparison.OrdinalIgnoreCase))
            {
                action = WinnersActionKind.Dec;
                return true;
            }

            if (string.Equals(s, "admin:winners:ok", StringComparison.OrdinalIgnoreCase))
            {
                action = WinnersActionKind.Ok;
                return true;
            }

            if (string.Equals(s, "admin:winners:noop", StringComparison.OrdinalIgnoreCase))
            {
                action = WinnersActionKind.Noop;
                return true;
            }

            return false;
        }

        internal static bool TryParseAdminDraw(string data, out int contestId)
        {
            contestId = 0;
            if (string.IsNullOrWhiteSpace(data)) return false;

            var s = data.Trim();
            if (!s.StartsWith("admin_draw:", StringComparison.OrdinalIgnoreCase)) return false;

            var parts = s.Split(':');
            if (parts.Length != 2) return false;

            return int.TryParse(parts[1], out contestId);
        }

        internal static bool TryParseContestsScreen(string data, out string screen, out string origin)
        {
            screen = null;
            origin = null;

            if (string.IsNullOrWhiteSpace(data)) return false;

            var s = data.Trim();
            // ожидаем: contests:<screen>:<origin>
            if (!s.StartsWith("contests:", StringComparison.OrdinalIgnoreCase))
                return false;

            var parts = s.Split(':');
            if (parts.Length != 3) return false;

            var sc = (parts[1] ?? "").Trim().ToLowerInvariant();
            var org = (parts[2] ?? "").Trim().ToLowerInvariant();

            if (sc != "menu" && sc != "mine" && sc != "active")
                return false;

            if (org != "back_menu" && org != "back_start")
                return false;

            screen = sc;
            origin = org;
            return true;
        }
    }
}

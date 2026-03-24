namespace ContestBot.Admin.Creation
{
    internal static class ContestCreationRules
    {
        internal const int WinnersMin = 1;
        internal const int WinnersMax = 20;
        internal const int WinnersDefault = 1;

        internal static int ClampWinners(int value)
        {
            if (value < WinnersMin) return WinnersMin;
            if (value > WinnersMax) return WinnersMax;
            return value;
        }

        /// <summary>
        /// If current value is less than min (e.g. 0), sets default, then clamps.
        /// This matches constructor UX: empty/invalid => default (3), not 1.
        /// </summary>
        internal static int EnsureWinnersInitialized(int current)
        {
            int v = current;
            if (v < WinnersMin) v = WinnersDefault;
            return ClampWinners(v);
        }

        internal static int ApplyDelta(int current, int delta)
        {
            return ClampWinners(current + delta);
        }
    }
}

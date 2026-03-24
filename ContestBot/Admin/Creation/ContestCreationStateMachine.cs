using System;

namespace ContestBot.Admin.Creation
{
    /// <summary>
    /// Чистая логика переходов конструктора (без Telegram).
    /// Её удобно покрывать unit-тестами.
    /// </summary>
    internal static class ContestCreationStateMachine
    {
        internal enum WinnersApplyResult
        {
            InvalidInput,
            Accepted
        }

        internal static WinnersApplyResult ApplyWinnersCountText(
            ContestCreationStore.Session session,
            string text,
            out int winnersValue)
        {
            winnersValue = 0;
            if (session == null || session.Draft == null) return WinnersApplyResult.InvalidInput;

            int winners;
            if (!int.TryParse((text ?? "").Trim(), out winners))
            {
                winnersValue = ContestCreationRules.EnsureWinnersInitialized(session.Draft.WinnersCount);
                session.Draft.WinnersCount = winnersValue;
                return WinnersApplyResult.InvalidInput;
            }

            winners = ContestCreationRules.ClampWinners(winners);
            session.Draft.WinnersCount = winners;
            winnersValue = winners;

            session.State = ContestCreationState.WaitDrawDateTime;
            return WinnersApplyResult.Accepted;
        }

        internal static bool ApplyDrawDateTimeText(
            ContestCreationStore.Session session,
            string text,
            Func<string, DateTime?> parseDateTime)
        {
            if (session == null || session.Draft == null) return false;
            if (parseDateTime == null) return false;

            var dt = parseDateTime(text ?? "");
            if (!dt.HasValue) return false;

            session.Draft.EndAt = dt.Value;
            session.Draft.StartAt = DateTime.Now;
            session.Draft.Status = "Draft";

            session.State = ContestCreationState.Preview;
            return true;
        }

        public static ContestCreationState DetectNextState(Contest draft)
        {
            if (draft == null)
                return ContestCreationState.WaitType;

            if (string.IsNullOrWhiteSpace(draft.Type))
                return ContestCreationState.WaitType;

            if (string.Equals(draft.Type, "referral", StringComparison.OrdinalIgnoreCase))
            {
                // пресет считается выбранным, если задан PerReferralWeight > 0 и MaxWeight > 1
                if (draft.PerReferralWeight <= 0 || draft.MaxWeight <= 1)
                    return ContestCreationState.WaitReferralPreset;
            }

            if (string.IsNullOrWhiteSpace(draft.Name))
                return ContestCreationState.WaitName;

            if (string.IsNullOrWhiteSpace(draft.Description))
                return ContestCreationState.WaitDescription;

            if (string.IsNullOrWhiteSpace(draft.MediaType))
                return ContestCreationState.WaitMedia;

            if (draft.WinnersCount <= 0)
                return ContestCreationState.WaitWinnersCount;

            if (draft.EndAt == default(DateTime))
                return ContestCreationState.WaitDrawDateTime;

            return ContestCreationState.Preview;
        }
    }
}
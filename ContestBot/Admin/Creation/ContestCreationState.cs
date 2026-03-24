namespace ContestBot.Admin.Creation
{
    internal enum ContestCreationState
    {
        None,
        WaitType,
        WaitReferralPreset,
        WaitName,
        WaitDescription,
        WaitMedia,
        WaitWinnersCount,
        WaitDrawDateTime,
        Preview,

        // редактирование из превью
        EditName,
        EditDescription,
        EditMedia,
        EditWinnersCount,
        EditDrawDateTime,
        EditTypePick,
        EditReferralPreset
    }
}

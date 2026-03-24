namespace ContestBot.Admin.Manage
{
    internal enum ContestManageState
    {
        None,
        WaitName,
        WaitDescription,
        WaitDate,
        WaitWinnersCount,
        WaitMedia
    }
}
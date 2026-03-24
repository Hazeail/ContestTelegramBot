namespace ContestBot.Admins
{
    internal enum AdminsManagementMode
    {
        None = 0,
        WaitingForwardForAdd = 1,
        WaitingForwardForOff = 2
    }

    internal sealed class AdminsManagementStore
    {
        public AdminsManagementMode Mode { get; set; } = AdminsManagementMode.None;
    }
}
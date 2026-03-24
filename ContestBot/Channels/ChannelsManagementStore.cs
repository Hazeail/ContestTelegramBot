namespace ContestBot.Channels
{
    internal enum ChannelsManagementMode
    {
        None = 0,
        WaitingForwardForAdd = 1
    }

    internal sealed class ChannelsManagementStore
    {
        public ChannelsManagementMode Mode { get; set; } = ChannelsManagementMode.None;
    }
}
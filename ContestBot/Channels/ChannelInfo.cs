using System;

namespace ContestBot.Channels
{
    internal sealed class ChannelInfo
    {
        public long ChannelId { get; set; }
        public string Username { get; set; } // без "@"
        public string Title { get; set; }
        public bool IsActive { get; set; }

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Username))
                return Title + " (@" + Username + ")";

            if (!string.IsNullOrWhiteSpace(Title))
                return Title;

            if (!string.IsNullOrWhiteSpace(Username))
                return "@" + Username;

            return ChannelId.ToString();
        }
    }
}
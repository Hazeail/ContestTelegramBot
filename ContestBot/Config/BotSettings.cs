namespace ContestBot.Config
{
    internal sealed class BotSettings
    {
        public string TokenFile { get; set; }
        public string Token { get; set; }
        public string BotUsername { get; set; }
        public long ChannelId { get; set; }
        public long AdminUserId { get; set; }
        public bool TimersEnabled { get; set; }
        public bool AutoDrawEnabled { get; set; }
        public string ConsoleLogLevel { get; set; }  // "INFO" | "WARN" | "ERROR"
        public string FileLogLevel { get; set; }     // "INFO" | "WARN" | "ERROR"
    }
}

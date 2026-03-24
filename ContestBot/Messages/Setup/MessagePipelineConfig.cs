using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Admin.Creation;
using ContestBot.Admin.Manage;

namespace ContestBot.Messages.Setup
{
    internal sealed class MessagePipelineConfig
    {
        public Func<long, bool> IsAdmin { get; set; }

        public Func<ITelegramBotClient, Message, CancellationToken, Task> HandleStartAsync { get; set; }
        public Func<ITelegramBotClient, Message, string, CancellationToken, Task> HandleStartWithArgAsync { get; set; }

        public Func<ITelegramBotClient, Message, string, CancellationToken, Task<bool>> HandleAdminCommandsAsync { get; set; }
        public Func<ITelegramBotClient, Message, string, CancellationToken, Task<bool>> HandleAdminChannelsAsync { get; set; }

        public ContestManageStore ManageStore { get; set; }
        public Func<ITelegramBotClient, Message, string, CancellationToken, Task> HandleContestManageStepAsync { get; set; }

        public Func<ITelegramBotClient, Message, string, CancellationToken, Task> HandleCreationStepAsync { get; set; }

        public ContestCreationStore CreationStore { get; set; }
    }
}

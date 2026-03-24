using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Messages.Actions;
using ContestBot.Admin.Manage;

namespace ContestBot.Messages.Setup
{
    internal static class MessagePipelineFactory
    {
        public static MessageUpdateHandler Create(MessagePipelineConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            var router = new MessageRouter();

            router.Add(new UserStartWithRefMessagesAction(cfg.HandleStartWithArgAsync));
            router.Add(new UserStartMessagesAction(cfg.HandleStartAsync));

            router.Add(new AdminContestManageMessagesAction(
                cfg.IsAdmin,
                cfg.ManageStore,
                cfg.HandleContestManageStepAsync
            ));

            router.Add(new AdminContestCreationMessagesAction(
                cfg.IsAdmin,
                cfg.CreationStore,
                cfg.HandleCreationStepAsync
            ));

            router.Add(new AdminChannelsMessagesAction(cfg.IsAdmin, cfg.HandleAdminChannelsAsync));

            router.Add(new AdminCommandsMessagesAction(cfg.IsAdmin, cfg.HandleAdminCommandsAsync));

            return new MessageUpdateHandler(router);
        }
    }
}

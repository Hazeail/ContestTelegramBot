using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Admins;
using ContestBot.Channels;
using ContestBot.Admin.Creation;
using ContestBot.Admin.Manage;
using ContestBot.Services;

namespace ContestBot.Callbacks.Setup
{
    internal sealed class CallbackPipelineConfig
    {
        // user ctx
        public Action<long, int> SetLastContestIdForUser { get; set; }

        public Func<ITelegramBotClient, Contest, CancellationToken, Task> TryUpdateChannelPostAsync { get; set; }
        public Func<ITelegramBotClient, Contest, CancellationToken, Task<ChannelPostUpdateResult>> RepostChannelPostWithResultAsync { get; set; }

        public Func<ITelegramBotClient, long, long, Contest, int?, string, CancellationToken, Task> ShowUserMenuAsync { get; set; }
        public Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> SendOrEditHtmlAsync { get; set; }

        public Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, Task> HandleActiveContestCardScreenAsync { get; set; }
        public Func<ITelegramBotClient, Message, CancellationToken, int?, int, string, string, Task> HandleFinishedContestCardScreenAsync { get; set; }
        public Func<ITelegramBotClient, Message, CancellationToken, int?, Task> HandleRefCommandAsync { get; set; }
        public Func<ITelegramBotClient, Message, CancellationToken, int?, Task> HandleMyRefCommandAsync { get; set; }

        public Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> HandleContestsMenuScreenAsync { get; set; }
        public Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> HandleMyContestsScreenAsync { get; set; }
        public Func<ITelegramBotClient, Message, CancellationToken, int?, string, Task> HandleActiveContestsScreenAsync { get; set; }

        // admin basics
        public long AdminUserId { get; private set; }
        public Func<long, bool> IsAdmin { get; set; }

        public ContestCreationStore CreationStore { get; set; }
        public ContestManageStore ManageStore { get; set; }
        public Action<AdminsManagementMode> SetAdminsManagementMode { get; set; }

        // admins ui
        public Func<ITelegramBotClient, long, int?, CancellationToken, Task> ShowAdminsListAsync { get; set; }
        public Func<ITelegramBotClient, long, int?, CancellationToken, Task> ShowAdminsAddInstructionAsync { get; set; }
        public Func<ITelegramBotClient, long, int?, CancellationToken, Task> ShowAdminsDisablePickListAsync { get; set; }

        // Channels
        public Action<ChannelsManagementMode> SetChannelsManagementMode { get; set; }

        public Func<ITelegramBotClient, long, int?, CancellationToken, Task> ShowChannelsListAsync { get; set; }
        public Func<ITelegramBotClient, long, int?, CancellationToken, Task> ShowChannelsAddInstructionAsync { get; set; }
        public Func<ITelegramBotClient, long, int?, CancellationToken, Task> ShowChannelsDisablePickListAsync { get; set; }

        public Func<IReadOnlyList<ChannelInfo>, InlineKeyboardMarkup> BuildAdminCreateChannelKb { get; set; }

        public Func<int> GetNextContestId { get; set; }

        public Func<InlineKeyboardMarkup> BuildAdminMenuKb { get; set; }
        public Func<InlineKeyboardMarkup> BuildAdminCreateTypeKb { get; set; }
        public Func<InlineKeyboardMarkup> BuildAdminCreateCancelKb { get; set; }
        public Func<InlineKeyboardMarkup> BuildAdminReferralPresetKb { get; set; }
        public Func<InlineKeyboardMarkup> BuildSkipMediaKb { get; set; }
        public Func<int, InlineKeyboardMarkup> BuildWinnersCountKb { get; set; }

        public Func<InlineKeyboardMarkup> BuildAdminCreateEditMenuKb { get; set; }
        public Func<InlineKeyboardMarkup> BuildAdminEditTypeKb { get; set; }
        public Func<InlineKeyboardMarkup> BuildAdminEditReferralPresetKb { get; set; }


        public Func<ITelegramBotClient, long, int, CancellationToken, Task> ShowAdminMenuAsync { get; set; }
        public Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task<int>> DeleteAndSendHtmlAsync { get; set; }
        public Func<ITelegramBotClient, Contest, CancellationToken, Task> PublishContestToChannelAsync { get; set; }
        public Func<ITelegramBotClient, long, long, int, string, CancellationToken, Task> ShowCreationPreviewAsync { get; set; }

        // admin draw
        public Func<ITelegramBotClient, long, int, int?, CancellationToken, Task> RunAdminDrawAsync { get; set; }

        public CallbackPipelineConfig(long adminUserId)
        {
            AdminUserId = adminUserId;
        }
    }
}

using ContestBot.Admin.Channels;
using ContestBot.Admin.Commands;
using ContestBot.Admin.Creation;
using ContestBot.Admin.Draw;
using ContestBot.Admin.Ui;
using ContestBot.Admins;
using ContestBot.Channels;
using ContestBot.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ContestBot.Composition
{
    internal sealed class UpdateHandlerComposerResult
    {
        public AdminAdminsUiHandler AdminAdminsUi { get; set; }
        public AdminChannelsUiHandler AdminChannelsUi { get; set; }
        public AdminChannelsMessagesHandler AdminChannelsMessages { get; set; }

        public AdminDirectory AdminDirectory { get; set; }

        public ContestTimersService Timers { get; set; }
        public AdminDrawHandler AdminDraw { get; set; }

        public AdminContestCreationMessagesHandler CreationMessages { get; set; }
        public AdminCommandsHandler AdminCommands { get; set; }

        public ContestBot.User.UserUiHandler UserUi { get; set; }
        public ContestBot.User.UserCommandsHandler UserCommands { get; set; }
        public ContestBot.User.Start.StartHandler Start { get; set; }
    }

    internal static class UpdateHandlerComposer
    {
        public static UpdateHandlerComposerResult Compose(
            BotCompositionRoot root,
            AdminsManagementStore adminsManagement,
            ChannelsManagementStore channelsManagement,
            Func<int?> getAdminPanelMsgId,

            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showAdminMenuAsync,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showChannelsListAsync,
            Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> showChannelsAddInstructionAsync,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showAdminsListAsync,
            Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> showAdminsAddInstructionAsync)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (adminsManagement == null) throw new ArgumentNullException(nameof(adminsManagement));
            if (channelsManagement == null) throw new ArgumentNullException(nameof(channelsManagement));
            if (getAdminPanelMsgId == null) throw new ArgumentNullException(nameof(getAdminPanelMsgId));
            if (showAdminsAddInstructionAsync == null) throw new ArgumentNullException(nameof(showAdminsAddInstructionAsync));
            if (showChannelsAddInstructionAsync == null) throw new ArgumentNullException(nameof(showChannelsAddInstructionAsync));

            var adminAdminsUi = new AdminAdminsUiHandler(
                root.Settings.AdminUserId,
                root.Kb.BuildAdminAdminsKeyboard,
                root.Kb.BuildAdminAdminsWaitKeyboard,
                root.Kb.BuildAdminAdminsDisablePickKeyboard
            );

            var adminChannelsUi = new AdminChannelsUiHandler(
                root.Kb.BuildAdminChannelsKeyboard,
                root.Kb.BuildAdminChannelsWaitKeyboard,
                root.Kb.BuildAdminChannelsDisablePickKeyboard
            );

            var adminChannelsMessages = new AdminChannelsMessagesHandler(
                root.Settings.AdminUserId,
                channelsManagement,
                getAdminPanelMsgId,
                (bot, chatId, editId, ct) => showChannelsListAsync(bot, chatId, editId, ct),
                (bot, chatId, editId, problem, ct) => showChannelsAddInstructionAsync(bot, chatId, editId, problem, ct)
            );

            // === Rights ===
            var adminDirectory = new AdminDirectory(root.Settings.AdminUserId);

            // === Draw / timers ===
            var drawService = root.DrawService;// (важно) тут сигнатура как в твоём текущем проекте bot_0.1:
            var timers = new ContestTimersService(root.ContestManager, drawService, root.Settings.AdminUserId, adminDirectory.IsAdmin);

            var adminDraw = new AdminDrawHandler(
                root.ContestManager,
                root.Ui.SendOrEditHtmlAsync,
                (bot, ct) => drawService.DoDrawWithOutcomeAsync(bot, ct),
                root.Settings.AdminUserId,
                adminDirectory.IsAdmin
            );

            // === Creation messages ===
            var creationMessages = new AdminContestCreationMessagesHandler(
                 root.CreationStore,
                 root.Dt.ParseNullable,
                 root.Kb.BuildAdminCreateCancelKeyboard,
                 root.Kb.BuildSkipMediaKeyboard,
                 root.Kb.BuildPreviewKeyboard,
                 root.Kb.BuildWinnersCountKeyboard,
                 root.ChannelPosts.BuildContestCaption,
                 root.Ui.DeleteAndSendHtmlAsync,
                 root.Ui.DeleteAndSendContestAsync
             );

            // === Admin commands ===
            var adminCommands = new AdminCommandsHandler(
                root.Settings.AdminUserId,
                adminsManagement,
                (bot, chatId, editId, ct) => showAdminsListAsync(bot, chatId, editId, ct),
                (bot, chatId, editId, problem, ct) => showAdminsAddInstructionAsync(bot, chatId, editId, problem, ct),
                getAdminPanelMsgId,
                (bot, chatId, editId, ct) => showAdminMenuAsync(bot, chatId, editId, ct)
            );

            // === User UI / commands / start ===
            var userUi = new ContestBot.User.UserUiHandler(
                Database.GetLastContestIdForUser,
                id => root.ContestManager.GetContestById(id),
                root.Ui.SendOrEditHtmlAsync
            );

            var userCommands = new ContestBot.User.UserCommandsHandler(
                root.ContestManager,
                root.Participants,
                Database.GetLastContestIdForUser,
                Database.SetLastContestIdForUser,
                root.Ui.SendOrEditHtmlAsync,
                root.Settings.BotUsername
            );

            var start = new ContestBot.User.Start.StartHandler(
                root.ContestManager,
                root.Participants,
                root.ChannelPosts,
                root.Subs,
                Database.GetLastContestIdForUser,
                Database.SetLastContestIdForUser,
                (bot, chatId, userId, contest, editId, header, ct)
                    => userUi.ShowUserMenuAsync(bot, chatId, userId, contest, editId, header, ct),
                (bot, chatId, editId, header, ct)
                    => userUi.ShowNeutralStartAsync(bot, chatId, editId, header, ct),
                (bot, msg, ct, editId, contestId, back, note)
                    => userCommands.HandleFinishedContestCardScreenAsync(bot, msg, ct, editId, contestId, back, note)
            );

            return new UpdateHandlerComposerResult
            {
                AdminAdminsUi = adminAdminsUi,
                AdminChannelsUi = adminChannelsUi,
                AdminChannelsMessages = adminChannelsMessages,

                AdminDirectory = adminDirectory,

                Timers = timers,
                AdminDraw = adminDraw,

                CreationMessages = creationMessages,
                AdminCommands = adminCommands,

                UserUi = userUi,
                UserCommands = userCommands,
                Start = start
            };
        }
    }
}
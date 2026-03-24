using ContestBot.Callbacks;
using ContestBot.Callbacks.Setup;
using ContestBot.Messages;
using ContestBot.Messages.Setup;
using ContestBot.Admin.Creation;
using ContestBot.Channels;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ContestBot
{
    internal sealed partial class UpdateHandler
    {
        private void BuildPipelines(
            out MessageUpdateHandler messageUpdateHandler,
            out CallbackUpdateHandler callbackUpdateHandler)
        {
            // --- Pipelines ---
            messageUpdateHandler = MessagePipelineFactory.Create(
                new MessagePipelineConfig()
                {
                    IsAdmin = _adminDirectory.IsAdmin,
                    HandleStartAsync = (bot, msg, ct) => _start.HandleStartAsync(bot, msg, ct),
                    HandleStartWithArgAsync = (bot, msg, arg, ct) => _start.HandleStartWithArgAsync(bot, msg, arg, ct),

                    ManageStore = _contestManageStore,
                    HandleContestManageStepAsync = (bot, msg, text, ct) => _contestManageMessages.HandleStepAsync(bot, msg, text, ct),

                    HandleAdminCommandsAsync = (bot, msg, text, ct) => _adminCommands.TryHandleAsync(bot, msg, text, ct),
                    HandleAdminChannelsAsync = (bot, msg, text, ct) => _adminChannelsMessages.TryHandleAsync(bot, msg, text, ct),

                    CreationStore = _root.CreationStore,
                    HandleCreationStepAsync = (bot, msg, text, ct) => _creationMessages.HandleStepAsync(bot, msg, text, ct)
                }
            );

            callbackUpdateHandler = CallbackPipelineFactory.Create(
                new CallbackPipelineConfig(_root.Settings.AdminUserId)
                {
                    IsAdmin = _adminDirectory.IsAdmin,

                    SetLastContestIdForUser = Database.SetLastContestIdForUser,

                    TryUpdateChannelPostAsync = (bot, contest, ct) => _root.ChannelPosts.TryUpdateChannelPostAsync(bot, contest, ct),
                    RepostChannelPostWithResultAsync = (bot, contest, ct) => _root.ChannelPosts.RepostChannelPostWithResultAsync(bot, contest, ct),

                    ShowUserMenuAsync = (bot, chatId, userId, contest, editId, header, ct)
                        => _userUi.ShowUserMenuAsync(bot, chatId, userId, contest, editId, header, ct),

                    SendOrEditHtmlAsync = (bot, chatId, msgId, text, kb, ct)
                        => _root.Ui.SendOrEditHtmlAsync(bot, chatId, msgId, text, kb, ct),

                    HandleActiveContestCardScreenAsync = (bot, msg, ct, msgId, contestId, back)
                        => _userCommands.HandleActiveContestCardScreenAsync(bot, msg, ct, msgId, contestId, back),

                    HandleFinishedContestCardScreenAsync = (bot, msg, ct, msgId, contestId, back, note)
                        => _userCommands.HandleFinishedContestCardScreenAsync(bot, msg, ct, msgId, contestId, back, note),

                    HandleRefCommandAsync = (bot, msg, ct, msgId) => _userCommands.HandleRefCommandAsync(bot, msg, ct, msgId),
                    HandleMyRefCommandAsync = (bot, msg, ct, msgId) => _userCommands.HandleMyRefCommandAsync(bot, msg, ct, msgId),
                    HandleContestsMenuScreenAsync = (bot, msg, ct, msgId, origin) => _userCommands.HandleContestsMenuScreenAsync(bot, msg, ct, msgId, origin),
                    HandleMyContestsScreenAsync = (bot, msg, ct, msgId, origin) => _userCommands.HandleMyContestsScreenAsync(bot, msg, ct, msgId, origin),
                    HandleActiveContestsScreenAsync = (bot, msg, ct, msgId, origin) => _userCommands.HandleActiveContestsScreenAsync(bot, msg, ct, msgId, origin),

                    SetChannelsManagementMode = v => _channelsManagement.Mode = v,
                    ShowChannelsListAsync = (bot, chatId, msgId, ct) => ShowChannelsListAsync(bot, chatId, msgId, ct),
                    ShowChannelsAddInstructionAsync = (bot, chatId, msgId, ct) => ShowChannelsAddInstructionAsync(bot, chatId, msgId, ct),
                    ShowChannelsDisablePickListAsync = (bot, chatId, msgId, ct) => ShowChannelsDisablePickListAsync(bot, chatId, msgId, ct),

                    SetAdminsManagementMode = v => _adminsManagement.Mode = v,

                    ShowAdminsListAsync = (bot, chatId, msgId, ct) => ShowAdminsListAsync(bot, chatId, msgId, ct),
                    ShowAdminsAddInstructionAsync = (bot, chatId, msgId, ct) => ShowAdminsAddInstructionAsync(bot, chatId, msgId, ct),
                    ShowAdminsDisablePickListAsync = (bot, chatId, msgId, ct) => ShowAdminsDisablePickListAsync(bot, chatId, msgId, ct),

                    CreationStore = _root.CreationStore,
                    ManageStore = _contestManageStore,

                    GetNextContestId = () => Database.GetNextContestId(),

                    BuildAdminCreateChannelKb = _root.Kb.BuildAdminCreateChannelKeyboard,
                    BuildAdminMenuKb = _root.Kb.BuildAdminMenuKeyboard,
                    BuildAdminCreateTypeKb = _root.Kb.BuildAdminCreateTypeKeyboard,
                    BuildAdminCreateCancelKb = _root.Kb.BuildAdminCreateCancelKeyboard,
                    BuildAdminReferralPresetKb = _root.Kb.BuildAdminReferralPresetKeyboard,
                    BuildSkipMediaKb = _root.Kb.BuildSkipMediaKeyboard,
                    BuildWinnersCountKb = _root.Kb.BuildWinnersCountKeyboard,

                    BuildAdminCreateEditMenuKb = _root.Kb.BuildAdminCreateEditMenuKeyboard,
                    BuildAdminEditTypeKb = _root.Kb.BuildAdminEditTypeKeyboard,
                    BuildAdminEditReferralPresetKb = _root.Kb.BuildAdminEditReferralPresetKeyboard,

                    ShowAdminMenuAsync = (bot, chatId, msgId, ct) => ShowAdminMenuAsync(bot, chatId, msgId, ct),

                    DeleteAndSendHtmlAsync = (bot, chatId, msgId, html, kb, ct) => _root.Ui.DeleteAndSendHtmlAsync(bot, chatId, msgId, html, kb, ct),
                    PublishContestToChannelAsync = (bot, contest, ct) => _root.ChannelPosts.PublishContestToChannelAsync(bot, contest, ct),
                    ShowCreationPreviewAsync = (bot, chatId, adminId, msgId, header, ct)
                        => _creationMessages.ShowPreviewAsync(bot, chatId, adminId, msgId > 0 ? (int?)msgId : null, header, ct),

                    RunAdminDrawAsync = (bot, chatId, contestId, editId, ct) => _adminDraw.RunAdminDrawAsync(bot, chatId, contestId, editId, ct)
                }
            );
        }
    }
}
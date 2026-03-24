using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Callbacks.Actions;

namespace ContestBot.Callbacks.Setup
{
    internal static class CallbackPipelineFactory
    {
        public static CallbackUpdateHandler Create(CallbackPipelineConfig cfg)
        {
            var router = new CallbackRouter();

            Func<ITelegramBotClient, long, int, string, InlineKeyboardMarkup, CancellationToken, Task<int>> deleteAndSendHtmlAdapter =
                (bot, chatId, oldMsgId, html, kb, ct) => cfg.DeleteAndSendHtmlAsync(bot, chatId, oldMsgId > 0 ? (int?)oldMsgId : null, html, kb, ct);

            // admin
            router.Add(new AdminCallbacksAction(
                cfg.IsAdmin,
                cfg.CreationStore,
                cfg.GetNextContestId,

                cfg.ManageStore,

                cfg.BuildAdminMenuKb,
                cfg.BuildAdminCreateTypeKb,
                cfg.BuildAdminCreateCancelKb,
                cfg.BuildAdminReferralPresetKb,
                cfg.BuildSkipMediaKb,
                cfg.BuildWinnersCountKb,
                cfg.BuildAdminCreateChannelKb,

                cfg.BuildAdminCreateEditMenuKb,
                cfg.BuildAdminEditTypeKb,
                cfg.BuildAdminEditReferralPresetKb,


                cfg.ShowAdminMenuAsync,
                deleteAndSendHtmlAdapter,
                cfg.PublishContestToChannelAsync,
                (bot, userId, text, ct) => bot.SendMessage(userId, text, cancellationToken: ct),
                cfg.TryUpdateChannelPostAsync,
                cfg.RepostChannelPostWithResultAsync,

                cfg.AdminUserId,
                cfg.ShowCreationPreviewAsync
            ));

            // user nav
            router.Add(new UserContestsScreensAction(
                cfg.HandleContestsMenuScreenAsync,
                cfg.HandleMyContestsScreenAsync,
                cfg.HandleActiveContestsScreenAsync
            ));

            router.Add(new UserContestCardCallbacksAction(
                cfg.HandleActiveContestCardScreenAsync
            ));

            router.Add(new UserFinishedContestCardCallbacksAction(
                cfg.HandleFinishedContestCardScreenAsync
            ));

            router.Add(new UserNavCallbacksAction(
                cfg.SetLastContestIdForUser,
                cfg.SendOrEditHtmlAsync,
                cfg.HandleRefCommandAsync,
                cfg.HandleMyRefCommandAsync,
                async (bot, chatId, userId, editMsgId, ct) =>
                {
                    await cfg.ShowUserMenuAsync(bot, chatId, userId, null, editMsgId, null, ct);
                }
            ));

            router.Add(new AdminDrawCallbacksAction(
                cfg.IsAdmin,
                cfg.RunAdminDrawAsync
            ));

            router.Add(new AdminAdminsCallbacksAction(
                cfg.IsAdmin,
                cfg.AdminUserId,
                cfg.SetAdminsManagementMode,
                cfg.ShowAdminsListAsync,
                cfg.ShowAdminsAddInstructionAsync,
                cfg.ShowAdminsDisablePickListAsync
            ));

            router.Add(new AdminChannelsCallbacksAction(
                cfg.IsAdmin,
                cfg.AdminUserId,
                cfg.SetChannelsManagementMode,
                cfg.ShowChannelsListAsync,
                cfg.ShowChannelsAddInstructionAsync,
                cfg.ShowChannelsDisablePickListAsync
            ));

            return new CallbackUpdateHandler(router);
        }
    }
}

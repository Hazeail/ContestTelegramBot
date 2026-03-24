using ContestBot.Admin.Commands;
using ContestBot.Admin.Creation;
using ContestBot.Admin.Draw;
using ContestBot.Admin.Ui;
using ContestBot.Callbacks;
using ContestBot.Composition;
using ContestBot.Messages;
using ContestBot.Services;
using ContestBot.Config;
using ContestBot.Admins;
using ContestBot.Channels;
using ContestBot.Admin.Channels;
using ContestBot.Admin.Manage;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot
{
    internal sealed partial class UpdateHandler
    {
        private readonly BotCompositionRoot _root;

        private readonly MessageUpdateHandler _messageUpdateHandler;
        private readonly CallbackUpdateHandler _callbackUpdateHandler;

        private readonly AdminAdminsUiHandler _adminAdminsUi;
        private readonly AdminsManagementStore _adminsManagement = new AdminsManagementStore();

        private readonly AdminChannelsUiHandler _adminChannelsUi;
        private readonly ChannelsManagementStore _channelsManagement = new ChannelsManagementStore();
        private readonly AdminChannelsMessagesHandler _adminChannelsMessages;

        private readonly AdminContestCreationMessagesHandler _creationMessages;
        private readonly ContestTimersService _timers;
        private readonly AdminDrawHandler _adminDraw;
        private readonly AdminCommandsHandler _adminCommands;
        private readonly ContestBot.Admins.AdminDirectory _adminDirectory;

        private readonly ContestManageStore _contestManageStore = new ContestManageStore();
        private readonly AdminContestManageMessagesHandler _contestManageMessages;

        private readonly ContestBot.User.UserUiHandler _userUi;
        private readonly ContestBot.User.UserCommandsHandler _userCommands;
        private readonly ContestBot.User.Start.StartHandler _start;


        private int? _adminPanelMessageId = null;

        private async Task DeleteAndSendAdminPanelHtmlAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? oldMessageId,
            string html,
            InlineKeyboardMarkup replyMarkup,
            CancellationToken token)
        {
            var newId = await _root.Ui.DeleteAndSendHtmlAsync(botClient, chatId, oldMessageId, html, replyMarkup, token);
            _adminPanelMessageId = newId;
        }

        public UpdateHandler(BotSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _root = new BotCompositionRoot(settings);

            var composed = UpdateHandlerComposer.Compose(
                _root,
                _adminsManagement,
                _channelsManagement,
                () => _adminPanelMessageId,

                // show screens
                (bot, chatId, editId, ct) => ShowAdminMenuAsync(bot, chatId, editId, ct),

                // Channels: message-handler flow => panel always below
                (bot, chatId, editId, ct) =>
                {
                    return _adminChannelsUi.ShowChannelsListAsync(bot, chatId, editId, DeleteAndSendAdminPanelHtmlAsync, ct);
                },
                (bot, chatId, editId, problem, ct) =>
                {
                    return _adminChannelsUi.ShowAddInstructionAsync(bot, chatId, editId, problem, DeleteAndSendAdminPanelHtmlAsync, ct);
                },

                // Admins: message-handler flow => panel always below
                (bot, chatId, editId, ct) =>
                {
                    return _adminAdminsUi.ShowAdminsListAsync(bot, chatId, editId, DeleteAndSendAdminPanelHtmlAsync, ct);
                },
                (bot, chatId, editId, problem, ct) =>
                {
                    return _adminAdminsUi.ShowAddInstructionAsync(bot, chatId, editId, problem, DeleteAndSendAdminPanelHtmlAsync, ct);
                }
            );

            _adminAdminsUi = composed.AdminAdminsUi;
            _adminChannelsUi = composed.AdminChannelsUi;
            _adminChannelsMessages = composed.AdminChannelsMessages;

            _adminDirectory = composed.AdminDirectory;

            _timers = composed.Timers;
            _adminDraw = composed.AdminDraw;

            _contestManageMessages = new AdminContestManageMessagesHandler(
                _contestManageStore,
                _root.ChannelPosts,
                _root.Ui.DeleteAndSendHtmlAsync
            );

            _creationMessages = composed.CreationMessages;
            _adminCommands = composed.AdminCommands;

            _userUi = composed.UserUi;
            _userCommands = composed.UserCommands;
            _start = composed.Start;

            // --- Pipelines ---
            MessageUpdateHandler msgHandler;
            CallbackUpdateHandler cbHandler;
            BuildPipelines(out msgHandler, out cbHandler);

            _messageUpdateHandler = msgHandler;
            _callbackUpdateHandler = cbHandler;
        }

        // =========================
        //  UPDATE ROUTING
        // =========================
        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            try
            {
                Console.WriteLine($"[IN] {DateTime.Now:HH:mm:ss} type={update?.Type} msg={update?.Message?.Text} cb={update?.CallbackQuery?.Data}");

                // 1) Callbacks
                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    // ≈сли закрываем панель Ч очищаем €корь, чтобы дальше не редактировалось УзакрытоеФ сообщение.
                    if (string.Equals(update.CallbackQuery.Data, "admin:close", StringComparison.OrdinalIgnoreCase))
                        _adminPanelMessageId = null;

                    await _callbackUpdateHandler.HandleAsync(botClient, update.CallbackQuery, token);
                    return;
                }

                // 2) Messages
                if (update.Type != UpdateType.Message)
                    return;

                var msg = update.Message;
                if (msg == null || msg.From == null)
                    return;

                string text = msg.Text ?? msg.Caption ?? string.Empty;

                await _messageUpdateHandler.TryHandleAsync(botClient, msg, text, token);
            }
            catch (Exception ex)
            {
                string ctx =
                    $"type={update?.Type} " +
                    $"msgText={update?.Message?.Text} " +
                    $"cb={update?.CallbackQuery?.Data} " +
                    $"chatId={update?.Message?.Chat?.Id} " +
                    $"fromId={update?.Message?.From?.Id ?? update?.CallbackQuery?.From?.Id}";

                Console.WriteLine("[EX] " + ex);
                _root.Crash.Error(ctx, ex);
            }
        }
        private async Task ShowAdminMenuAsync(ITelegramBotClient botClient, long chatId, int? editMessageId, CancellationToken token)
        {
            var text =
                "<b>јдмин-панель</b>\n\n" +
                "<u>–азделы</u>\n" +
                "Х <b>—оздание</b> Ч новый конкурс и черновики\n" +
                "Х <b> онкурсы</b> Ч список и карточка конкурса\n" +
                "Х <b>”правление</b> Ч админы и каналы";

            InlineKeyboardMarkup kb = _root.Kb.BuildAdminMenuKeyboard();

            if (editMessageId.HasValue)
            {
                _adminPanelMessageId = editMessageId.Value;
                await _root.Ui.SendOrEditHtmlAsync(botClient, chatId, editMessageId, text, kb, token);
                return;
            }

            var sent = await botClient.SendMessage(
                chatId,
                text,
                parseMode: ParseMode.Html,
                replyMarkup: kb,
                cancellationToken: token);

            _adminPanelMessageId = sent.MessageId;
        }
    }
}

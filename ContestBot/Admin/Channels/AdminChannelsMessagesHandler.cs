using System;
using System.Threading;
using System.Threading.Tasks;
using ContestBot.Channels;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ContestBot.Admin.Channels
{
    internal sealed class AdminChannelsMessagesHandler
    {
        private readonly long _superAdminUserId;
        private readonly ChannelsManagementStore _store;

        private readonly Func<int?> _getAdminPanelMessageId;
        private readonly Func<ITelegramBotClient, long, int?, CancellationToken, Task> _showChannelsListAsync;
        private readonly Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> _showChannelsAddInstructionAsync;

        public AdminChannelsMessagesHandler(
            long superAdminUserId,
            ChannelsManagementStore store,
            Func<int?> getAdminPanelMessageId,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showChannelsListAsync,
            Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> showChannelsAddInstructionAsync)
        {
            _superAdminUserId = superAdminUserId;
            _store = store;
            _getAdminPanelMessageId = getAdminPanelMessageId;
            _showChannelsListAsync = showChannelsListAsync;
            _showChannelsAddInstructionAsync = showChannelsAddInstructionAsync;
        }

        public async Task<bool> TryHandleAsync(
            ITelegramBotClient botClient,
            Message msg,
            string text,
            CancellationToken token)
        {
            if (msg?.From == null) return false;
            if (_store == null) return false;
            if (_store.Mode == ChannelsManagementMode.None) return false;

            long chatId = msg.Chat.Id;

            // только супер-админ
            if (msg.From.Id != _superAdminUserId)
            {
                _store.Mode = ChannelsManagementMode.None;

                var panelId = _getAdminPanelMessageId();
                if (panelId.HasValue)
                    await _showChannelsListAsync(botClient, chatId, panelId.Value, token);

                return true;
            }

            if (_store.Mode == ChannelsManagementMode.WaitingForwardForAdd)
            {
                var panelId = _getAdminPanelMessageId();

                // Нужно пересланное сообщение ИЗ КАНАЛА
                var fwdChat = msg.ForwardFromChat;

                if (fwdChat == null)
                {
                    if (panelId.HasValue)
                        await _showChannelsAddInstructionAsync(
                            botClient, chatId, panelId.Value,
                            "⚠️ Не получилось. Пришли сюда пост из канала через «Переслать».",
                            token);

                    return true; // остаёмся в ожидании
                }

                if (fwdChat.Type != ChatType.Channel)
                {
                    if (panelId.HasValue)
                        await _showChannelsAddInstructionAsync(
                            botClient, chatId, panelId.Value,
                            "⚠️ Это не пост из канала. Перешли любой пост из нужного канала.",
                            token);

                    return true;
                }

                Database.UpsertChannel(
                    fwdChat.Id,
                    fwdChat.Username,
                    fwdChat.Title,
                    _superAdminUserId
                );

                // выходим из режима добавления
                _store.Mode = ChannelsManagementMode.None;

                // обновляем панель (редактируем сообщение панели)
                if (panelId.HasValue)
                    await _showChannelsListAsync(botClient, chatId, panelId.Value, token);

                return true;
            }

            _store.Mode = ChannelsManagementMode.None;
            return true;
        }
    }
}
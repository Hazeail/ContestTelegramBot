using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContestBot.Admins;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ContestBot.Admin.Commands
{
    internal sealed class AdminCommandsHandler
    {
        private readonly long _adminUserId;

        private readonly Func<int?> _getAdminPanelMessageId;

        private readonly Func<ITelegramBotClient, long, int?, CancellationToken, Task> _showAdminMenuAsync;

        private readonly AdminsManagementStore _adminsManagement;
        private readonly Func<ITelegramBotClient, long, int?, CancellationToken, Task> _showAdminsListAsync;
        private readonly Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> _showAdminsAddInstructionAsync;

        public AdminCommandsHandler(
            long adminUserId,
            AdminsManagementStore adminsManagement,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showAdminsListAsync,
            Func<ITelegramBotClient, long, int?, string, CancellationToken, Task> showAdminsAddInstructionAsync,
            Func<int?> getAdminPanelMessageId,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showAdminMenuAsync)
        {
            _adminUserId = adminUserId;
            _adminsManagement = adminsManagement;
            _showAdminsListAsync = showAdminsListAsync;
            _getAdminPanelMessageId = getAdminPanelMessageId;
            _showAdminMenuAsync = showAdminMenuAsync;
            _showAdminsAddInstructionAsync = showAdminsAddInstructionAsync;
        }

        public async Task<bool> TryHandleAsync(
            ITelegramBotClient botClient,
            Message msg,
            string text,
            CancellationToken token)
        {
            if (msg?.From == null) return false;

            long chatId = msg.Chat.Id;
            text = text ?? string.Empty;

            // 0) Управление админами (только супер-админ, только пересылкой сообщения)
            if (_adminsManagement != null && _adminsManagement.Mode != AdminsManagementMode.None)
            {
                // только супер-админ
                if (msg.From.Id != _adminUserId)
                {
                    _adminsManagement.Mode = AdminsManagementMode.None;

                    var panelId = _getAdminPanelMessageId();
                    if (panelId.HasValue)
                        await _showAdminMenuAsync(botClient, chatId, panelId.Value, token);

                    return true;
                }

                // строго: только forward, и обязательно должен быть ForwardFrom
                if (msg.ForwardFrom == null)
                {
                    var panelId = _getAdminPanelMessageId();
                    if (panelId.HasValue)
                        await _showAdminsAddInstructionAsync(
                            botClient, chatId, panelId.Value,
                            "⚠️ Не получилось. Перешли сообщение человека через «Переслать».",
                            token);

                    return true; // режим НЕ сбрасываем
                }

                long targetUserId = msg.ForwardFrom.Id;

                if (_adminsManagement.Mode == AdminsManagementMode.WaitingForwardForAdd)
                {
                    Database.UpsertAdminProfile(
                        targetUserId,
                        _adminUserId,
                        msg.ForwardFrom.Username,
                        msg.ForwardFrom.FirstName,
                        msg.ForwardFrom.LastName
                    );
                    _adminsManagement.Mode = AdminsManagementMode.None;

                    var panelId = _getAdminPanelMessageId();
                    if (panelId.HasValue)
                        await _showAdminsListAsync(botClient, chatId, panelId.Value, token);

                    return true;
                }


                // неизвестное состояние (на всякий случай)
                _adminsManagement.Mode = AdminsManagementMode.None;
                return true;
            }

            // Единственный вход в админ-панель
            if (string.Equals((text ?? string.Empty).Trim(), "/admin", StringComparison.OrdinalIgnoreCase))
            {
                // ВАЖНО: всегда открываем новым сообщением (не редактируем прошлую панель)
                await _showAdminMenuAsync(botClient, chatId, null, token);
                return true;
            }

            return false;
        }
    }
}

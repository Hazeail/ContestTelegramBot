using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ContestBot.Admins;

namespace ContestBot.Callbacks.Actions
{
    internal sealed class AdminAdminsCallbacksAction : ICallbackAction
    {
        private const string DisablePrefix = "admin:admins_disable:";

        private readonly Func<long, bool> _isAdmin;
        private readonly long _superAdminUserId;

        private readonly Action<AdminsManagementMode> _setMode;

        
        private readonly Func<ITelegramBotClient, long, int?, CancellationToken, Task> _showListAsync;
        private readonly Func<ITelegramBotClient, long, int?, CancellationToken, Task> _showAddInstructionAsync;
        private readonly Func<ITelegramBotClient, long, int?, CancellationToken, Task> _showDisablePickListAsync;

        public AdminAdminsCallbacksAction(
            Func<long, bool> isAdmin,
            long superAdminUserId,
            Action<AdminsManagementMode> setMode,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showListAsync,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showAddInstructionAsync,
            Func<ITelegramBotClient, long, int?, CancellationToken, Task> showDisablePickListAsync)
        {
            _isAdmin = isAdmin;
            _superAdminUserId = superAdminUserId;

            _setMode = setMode;

            _showListAsync = showListAsync;
            _showAddInstructionAsync = showAddInstructionAsync;
            _showDisablePickListAsync = showDisablePickListAsync;
        }

        public async Task<bool> TryHandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            if (cq?.Data == null) return false;

            bool isMine =
                cq.Data == "admin:admins" ||
                cq.Data == "admin:admins_add" ||
                cq.Data == "admin:admins_off" ||
                cq.Data == "admin:admins_cancel" ||
                cq.Data.StartsWith(DisablePrefix, StringComparison.Ordinal);

            if (!isMine) return false;

            long userId = cq.From.Id;
            long chatId = cq.Message?.Chat.Id ?? userId;
            int? editMessageId = cq.Message?.MessageId;

            if (!_isAdmin(userId))
            {
                await SafeAnswer(bot, cq.Id, "Недостаточно прав.", ct);
                return true;
            }

            if (userId != _superAdminUserId)
            {
                await SafeAnswer(bot, cq.Id, "Только главный админ может управлять админами.", ct);
                return true;
            }

            await SafeAnswer(bot, cq.Id, null, ct);

            if (cq.Data == "admin:admins_add")
            {
                _setMode(AdminsManagementMode.WaitingForwardForAdd);
                if (_showAddInstructionAsync != null)
                    await _showAddInstructionAsync(bot, chatId, editMessageId, ct);
                return true;
            }

            if (cq.Data == "admin:admins_off")
            {
                _setMode(AdminsManagementMode.None);
                if (_showDisablePickListAsync != null)
                    await _showDisablePickListAsync(bot, chatId, editMessageId, ct);
                return true;
            }

            if (cq.Data == "admin:admins_cancel")
            {
                _setMode(AdminsManagementMode.None);
                if (_showListAsync != null)
                    await _showListAsync(bot, chatId, editMessageId, ct);
                return true;
            }

            if (cq.Data.StartsWith(DisablePrefix, StringComparison.Ordinal))
            {
                _setMode(AdminsManagementMode.None);

                var part = cq.Data.Substring(DisablePrefix.Length);
                if (long.TryParse(part, out var targetUserId))
                {
                    if (targetUserId == _superAdminUserId)
                    {
                        await SafeAnswer(bot, cq.Id, "Супер-админа отключить нельзя.", ct);
                        return true;
                    }

                    Database.DeactivateAdmin(targetUserId);
                    await SafeAnswer(bot, cq.Id, "✅ Ок", ct);
                }

                if (_showListAsync != null)
                    await _showListAsync(bot, chatId, editMessageId, ct);

                return true;
            }

            // admin:admins
            _setMode(AdminsManagementMode.None);
            if (_showListAsync != null)
                await _showListAsync(bot, chatId, editMessageId, ct);

            return true;
        }

        private static async Task SafeAnswer(ITelegramBotClient bot, string cqId, string text, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    await bot.AnswerCallbackQuery(cqId, cacheTime: 0, cancellationToken: ct);
                else
                    await bot.AnswerCallbackQuery(cqId, text, cacheTime: 0, cancellationToken: ct);
            }
            catch { }
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgUser = Telegram.Bot.Types.User;

namespace ContestBot.Services
{
    internal sealed class OwnerNotificationService
    {
        private readonly Func<ITelegramBotClient, long, string, CancellationToken, Task> _sendPrivateAsync;
        private readonly long _superAdminUserId;
        private readonly Func<long, bool> _isAdmin;

        public OwnerNotificationService(
             Func<ITelegramBotClient, long, string, CancellationToken, Task> sendPrivateAsync,
             long superAdminUserId,
             Func<long, bool> isAdmin)
        {
            _sendPrivateAsync = sendPrivateAsync;
            _superAdminUserId = superAdminUserId;
            _isAdmin = isAdmin;
        }

        /// <summary>
        /// Простой сценарий: отправить ownerId, если не доставилось — fallback супер-админу.
        /// (Это заменяет ContestTimersService.SendToOwnerWithFallbackAsync)
        /// </summary>
        public async Task SendToOwnerWithFallbackAsync(
            ITelegramBotClient botClient,
            long ownerId,
            string text,
            CancellationToken token)
        {
            if (ownerId <= 0) return;

            // Если делегат не задан — используем прямой вызов (в проекте есть botClient.SendMessage)
            var sender = _sendPrivateAsync ?? new Func<ITelegramBotClient, long, string, CancellationToken, Task>(
                (b, chatId, msg, ct) => b.SendMessage(
                    chatId,
                    msg,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct
                )
            );

            Exception ownerSendError = null;

            // если создатель больше не активный админ — уведомляем главного админа
            if (_isAdmin != null &&
                ownerId > 0 &&
                ownerId != _superAdminUserId &&
                !_isAdmin(ownerId))
            {
                if (_superAdminUserId > 0)
                {
                    string payload = text + "\n\n<i>Создатель конкурса отключён как админ</i>";
                    try { await sender(botClient, _superAdminUserId, payload, token); }
                    catch { }
                }
                return;
            }

            // 1) пробуем владельцу
            try
            {
                await sender(botClient, ownerId, text, token);
                return;
            }
            catch (Exception ex)
            {
                ownerSendError = ex;
            }

            // 2) fallback супер-админу (если это не он)
            if (_superAdminUserId > 0 && _superAdminUserId != ownerId)
            {
                string payload = text;

                // Важно: дописываем только в fallback-сообщение
                if (ownerSendError != null)
                    payload += "\n\n<i>Владельцу отправить не удалось</i>";

                try { await sender(botClient, _superAdminUserId, payload, token); }
                catch { }
            }
        }

        /// <summary>
        /// Сценарий из админки: уведомить создателя конкурса (CreatedByAdminUserId),
        /// не дублируя если он и так в текущем чате, и с подписью "кто сделал действие".
        /// (Это заменяет AdminCallbacksAction.NotifyOwnerAsync)
        /// </summary>
        public async Task NotifyOwnerAsync(
            ITelegramBotClient botClient,
            long currentChatId,
            Contest contest,
            TgUser actor,
            string text,
            CancellationToken token)
        {
            if (contest == null) return;

            long ownerId = contest.CreatedByAdminUserId ?? 0;
            if (ownerId <= 0) ownerId = _superAdminUserId;
            if (ownerId <= 0) return;

            bool redirectedToSuper = false;

            if (_isAdmin != null &&
                ownerId > 0 &&
                ownerId != _superAdminUserId &&
                !_isAdmin(ownerId))
            {
                ownerId = _superAdminUserId;
                redirectedToSuper = true;
            }

            if (ownerId <= 0) return;

            string actorLine = "";
            if (actor != null && ownerId != actor.Id)
            {
                var name = !string.IsNullOrWhiteSpace(actor.Username)
                    ? "@" + actor.Username
                    : (string.IsNullOrWhiteSpace(actor.FirstName) ? actor.Id.ToString() : actor.FirstName);

                actorLine = "\n\n<i>Действие выполнил:</i> " + WebUtility.HtmlEncode(name);
            }

            string payload = text + actorLine;

            if (redirectedToSuper)
                payload += "\n\n<i>Создатель конкурса отключён как админ</i>";

            await SendToOwnerWithFallbackAsync(botClient, ownerId, payload, token);
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using ContestBot.Admin.Creation;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private async Task<bool> TryHandleCreateFlowAsync(
            ITelegramBotClient bot,
            Telegram.Bot.Types.CallbackQuery cq,
            long chatId,
            int msgId,
            CancellationToken ct)
        {
            long adminId = cq.From != null ? cq.From.Id : 0;
            var session = _creationStore.GetOrCreate(adminId);
            var now = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            if (cq.Data == "admin:create")
            {
                var draft = new Contest
                {
                    Id = 0, // Id назначится при publish (см. AdminCallbacksAction.Publish.cs)
                    Status = "Draft",
                    CreatedByAdminUserId = adminId
                };

                session.Draft = draft;
                session.DraftId = null; // ключевое: пока тип не выбран — черновика в БД нет
                session.State = ContestCreationState.WaitType;

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Создание конкурса 🛠</b>\n\nВыбери тип:",
                    _buildAdminCreateTypeKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:create_cancel")
            {
                DeactivateDraftInDb(session, adminId);
                session.Reset();
                await _showAdminMenuAsync(bot, chatId, msgId, ct);
                return true;
            }

            if (cq.Data == "admin:create_back_to_type")
            {
                if (session.Draft == null) return true;

                session.State = ContestCreationState.WaitType;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Создание конкурса 🛠</b>\n\nВыбери тип:",
                    _buildAdminCreateTypeKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:create_preview")
            {
                await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                return true;
            }

            if (cq.Data == "admin:create_back")
            {
                if (session == null || session.Draft == null)
                    return true;

                bool isEdit =
                    session.State == ContestCreationState.EditName ||
                    session.State == ContestCreationState.EditDescription ||
                    session.State == ContestCreationState.EditMedia ||
                    session.State == ContestCreationState.EditWinnersCount ||
                    session.State == ContestCreationState.EditDrawDateTime ||
                    session.State == ContestCreationState.EditTypePick ||
                    session.State == ContestCreationState.EditReferralPreset;

                if (isEdit)
                {
                    // уходим в меню изменений и ОБЯЗАТЕЛЬНО сбрасываем state
                    session.State = ContestCreationState.Preview;
                    SaveDraftToDb(session, adminId);

                    int panelId = await _deleteAndSendHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Изменить конкурс</b>\n\nВыбери, что изменить:",
                        _buildAdminCreateEditMenuKb(),
                        ct);

                    session.PanelMessageId = panelId;
                    return true;
                }

                // создание: назад ведёт в превью
                await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                return true;
            }

            if (cq.Data == "admin:create_type:norm")
            {
                if (session.Draft == null) return true;

                session.Draft.Type = "normal";
                session.Draft.BaseWeight = 1;
                session.Draft.PerReferralWeight = 0;
                session.Draft.MaxWeight = 1;

                session.State = ContestCreationState.WaitName;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Шаг 1/6 • Название</b>\n\nОтправь название конкурса одним сообщением.",
                    _buildAdminCreateCancelKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:create_type:ref")
            {
                if (session.Draft == null) return true;

                session.Draft.Type = "referral";
                session.Draft.BaseWeight = 1;

                session.State = ContestCreationState.WaitReferralPreset;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Реферальный конкурс</b>\n\nВыбери режим рефералов:",
                    _buildAdminReferralPresetKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data.StartsWith("admin:create_preset:", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Draft == null) return true;

                var parts = cq.Data.Split(':');
                if (parts.Length == 3)
                {
                    if (parts[2] == "1") { session.Draft.PerReferralWeight = 0.2; session.Draft.MaxWeight = 3; }
                    else if (parts[2] == "2") { session.Draft.PerReferralWeight = 0.3; session.Draft.MaxWeight = 7.5; }
                    else if (parts[2] == "3") { session.Draft.PerReferralWeight = 0.5; session.Draft.MaxWeight = 10; }
                }

                session.State = ContestCreationState.WaitName;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Шаг 1/6 • Название</b>\n\nОтправь название конкурса одним сообщением.",
                    _buildAdminCreateCancelKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:create_skip_media")
            {
                if (session.Draft == null) return true;

                if (session.State == ContestCreationState.EditMedia)
                {
                    session.Draft.MediaType = "none";
                    session.Draft.MediaFileId = null;
                    session.Draft.ImageFileId = null;

                    SaveDraftToDb(session, adminId);
                    await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                    return true;
                }

                session.Draft.MediaType = "none";
                session.Draft.MediaFileId = null;

                session.State = ContestCreationState.WaitWinnersCount;
                SaveDraftToDb(session, adminId);

                int winners = ContestCreationRules.EnsureWinnersInitialized(session.Draft.WinnersCount);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Шаг 4/6 • Призовые места</b>\n\nСколько будет призовых мест?\nМожно кнопками или отправь число сообщением.",
                    _buildWinnersCountKb(winners),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:create_preview")
            {
                await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                return true;
            }

            if (cq.Data == "admin:create_edit_menu")
            {
                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменить конкурс</b>\n\nВыбери, что изменить:",
                    _buildAdminCreateEditMenuKb(),
                    ct);

                session.State = ContestCreationState.Preview;
                SaveDraftToDb(session, adminId);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:name")
            {
                session.State = ContestCreationState.EditName;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменение • Название</b>\n\nОтправь новое название конкурса одним сообщением.",
                    _buildAdminCreateCancelKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:desc")
            {
                session.State = ContestCreationState.EditDescription;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменение • Описание</b>\n\nОтправь новое описание конкурса одним сообщением.",
                    _buildAdminCreateCancelKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:media")
            {
                session.State = ContestCreationState.EditMedia;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменение • Медиа</b>\n\nОтправь медиа (фото / GIF / видео) одним сообщением\nили нажми «Без медиа».",
                    _buildSkipMediaKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:winners")
            {
                if (session.Draft == null) return true;

                session.State = ContestCreationState.EditWinnersCount;
                SaveDraftToDb(session, adminId);

                int winners = ContestCreationRules.EnsureWinnersInitialized(session.Draft.WinnersCount);
                session.Draft.WinnersCount = winners;

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменение • Призовые места</b>\n\nМожно кнопками или отправь число сообщением.",
                    _buildWinnersCountKb(winners),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:date")
            {
                session.State = ContestCreationState.EditDrawDateTime;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменение • Дата розыгрыша</b>\n\nВведи дату и время розыгрыша.\nФормат: <code>" + now + "</code>",
                    _buildAdminCreateCancelKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:type")
            {
                session.State = ContestCreationState.EditTypePick;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Изменение • Тип</b>\n\nВыбери тип конкурса:",
                    _buildAdminEditTypeKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:type:norm")
            {
                if (session.Draft == null) return true;

                session.Draft.Type = "normal";
                session.Draft.BaseWeight = 1;
                session.Draft.PerReferralWeight = 0;
                session.Draft.MaxWeight = 1;

                SaveDraftToDb(session, adminId);

                await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                return true;
            }

            if (cq.Data == "admin:edit:type:ref")
            {
                if (session.Draft == null) return true;

                session.Draft.Type = "referral";
                session.Draft.BaseWeight = 1;

                session.State = ContestCreationState.EditReferralPreset;
                SaveDraftToDb(session, adminId);

                int panelId = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Режим рефералов</b>\n\nВыбери режим:",
                    _buildAdminEditReferralPresetKb(),
                    ct);

                session.PanelMessageId = panelId;
                return true;
            }

            if (cq.Data == "admin:edit:preset")
            {
                if (session.Draft == null) return true;

                if (!string.Equals(session.Draft.Type, "referral", StringComparison.OrdinalIgnoreCase))
                {
                    int panelId = await _deleteAndSendHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Режим рефералов</b>\n\nДоступно только для реферального конкурса.\nСначала измени тип на «Реферальный».",
                        _buildAdminCreateEditMenuKb(),
                        ct);

                    session.PanelMessageId = panelId;
                    return true;
                }

                session.State = ContestCreationState.EditReferralPreset;
                SaveDraftToDb(session, adminId);

                int panel2 = await _deleteAndSendHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Режим рефералов</b>\n\nВыбери режим:",
                    _buildAdminEditReferralPresetKb(),
                    ct);

                session.PanelMessageId = panel2;
                return true;
            }

            if (cq.Data.StartsWith("admin:edit:preset:", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Draft == null) return true;

                var parts = cq.Data.Split(':');
                if (parts.Length == 4)
                {
                    if (parts[3] == "1") { session.Draft.PerReferralWeight = 0.2; session.Draft.MaxWeight = 3; }
                    else if (parts[3] == "2") { session.Draft.PerReferralWeight = 0.3; session.Draft.MaxWeight = 7.5; }
                    else if (parts[3] == "3") { session.Draft.PerReferralWeight = 0.5; session.Draft.MaxWeight = 10; }
                }

                SaveDraftToDb(session, adminId);

                await _showCreationPreviewAsync(bot, chatId, adminId, msgId, null, ct);
                return true;
            }

            return false;
        }

    }
}
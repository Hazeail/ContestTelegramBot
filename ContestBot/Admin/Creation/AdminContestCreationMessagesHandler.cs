using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot.Admin.Creation
{
    internal sealed class AdminContestCreationMessagesHandler
    {
        private readonly ContestCreationStore _store;

        private readonly Func<string, DateTime?> _parseDateTime;

        private readonly Func<InlineKeyboardMarkup> _buildCancelKb;
        private readonly Func<InlineKeyboardMarkup> _buildSkipMediaKb;
        private readonly Func<InlineKeyboardMarkup> _buildPreviewKb;

        private readonly Func<int, InlineKeyboardMarkup> _buildWinnersCountKb;

        private readonly Func<Contest, int, string> _buildContestCaption;

        private readonly Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task<int>> _deleteAndSendHtmlAsync;
        private readonly Func<ITelegramBotClient, long, int?, Contest, string, InlineKeyboardMarkup, CancellationToken, Task<int>> _deleteAndSendContestAsync;

        public AdminContestCreationMessagesHandler(
            ContestCreationStore store,
            Func<string, DateTime?> parseDateTime,
            Func<InlineKeyboardMarkup> buildCancelKb,
            Func<InlineKeyboardMarkup> buildSkipMediaKb,
            Func<InlineKeyboardMarkup> buildPreviewKb,
            Func<int, InlineKeyboardMarkup> buildWinnersCountKb,
            Func<Contest, int, string> buildContestCaption,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task<int>> deleteAndSendHtmlAsync,
            Func<ITelegramBotClient, long, int?, Contest, string, InlineKeyboardMarkup, CancellationToken, Task<int>> deleteAndSendContestAsync)
        {
            _store = store;
            _parseDateTime = parseDateTime;
            _buildCancelKb = buildCancelKb;
            _buildSkipMediaKb = buildSkipMediaKb;
            _buildPreviewKb = buildPreviewKb;
            _buildWinnersCountKb = buildWinnersCountKb;
            _buildContestCaption = buildContestCaption;
            _deleteAndSendHtmlAsync = deleteAndSendHtmlAsync;
            _deleteAndSendContestAsync = deleteAndSendContestAsync;
        }

        public async Task HandleStepAsync(ITelegramBotClient botClient, Message msg, string text, CancellationToken token)
        {
            
            long chatId = msg.Chat.Id;
            var now = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            var adminId = msg.From.Id;
            var session = _store.GetOrCreate(adminId);
            var state = session.State;
            var draft = session.Draft;
            var panelId = session.PanelMessageId;

            if (session.Draft == null) return;
            if (session.State == ContestCreationState.Preview) return;

            switch (session.State)
            {
                case ContestCreationState.EditName:
                    session.Draft.Name = (text ?? "").Trim();

                    SaveStepToDb(session, adminId);
                    await ShowPreviewAsync(botClient, chatId, adminId, panelId, null, token);
                    return;

                case ContestCreationState.EditDescription:
                    session.Draft.Description = (text ?? "").Trim();

                    SaveStepToDb(session, adminId);
                    await ShowPreviewAsync(botClient, chatId, adminId, panelId, null, token);
                    return;

                case ContestCreationState.EditMedia:
                    {
                        string editMediaType = null;
                        string editFileId = null;


                        if (msg.Photo != null && msg.Photo.Length > 0)
                        {
                            editMediaType = "photo";
                            editFileId = msg.Photo[msg.Photo.Length - 1].FileId;
                        }
                        else if (msg.Animation != null)
                        {
                            editMediaType = "animation";
                            editFileId = msg.Animation.FileId;
                        }
                        else if (msg.Video != null)
                        {
                            editMediaType = "video";
                            editFileId = msg.Video.FileId;
                        }

                        if (string.IsNullOrEmpty(editFileId))
                        {
                            session.PanelMessageId = await _deleteAndSendHtmlAsync(
                                botClient, chatId, panelId,
                                "<b>Изменение • Медиа</b>\n\nЖду медиа: фото / GIF / видео.\n\nИли нажми «Без медиа».",
                                _buildSkipMediaKb(),
                                token);
                            return;
                        }

                        session.Draft.MediaType = editMediaType;
                        session.Draft.MediaFileId = editFileId;

                        if (editMediaType == "photo")
                            session.Draft.ImageFileId = editFileId;
                        else
                            session.Draft.ImageFileId = null;

                        SaveStepToDb(session, adminId);
                        await ShowPreviewAsync(botClient, chatId, adminId, panelId, null, token);
                        return;
                    }

                case ContestCreationState.EditWinnersCount:
                    {
                        int w;
                        if (!int.TryParse((text ?? "").Trim(), out w))
                        {
                            int curr = ContestCreationRules.EnsureWinnersInitialized(session.Draft.WinnersCount);
                            session.Draft.WinnersCount = curr;

                            session.PanelMessageId = await _deleteAndSendHtmlAsync(
                                botClient, chatId, panelId,
                                "<b>Изменение • Призовые места</b>\n\nНужно число от 1 до 20.\n\nМожно кнопками или отправь число сообщением.",
                                _buildWinnersCountKb(curr),
                                token);
                            return;
                        }

                        w = ContestCreationRules.ClampWinners(w);
                        session.Draft.WinnersCount = w;

                        SaveStepToDb(session, adminId);
                        await ShowPreviewAsync(botClient, chatId, adminId, panelId, null, token);
                        return;
                    }

                case ContestCreationState.WaitName:
                    session.Draft.Name = (text ?? "").Trim();
                    session.State = ContestCreationState.WaitDescription;

                    SaveStepToDb(session, adminId);

                    session.PanelMessageId = await _deleteAndSendHtmlAsync(
                        botClient, chatId, panelId,
                        "<b>Шаг 2/6 • Описание</b>\n\nОтправь описание конкурса одним сообщением.",
                        _buildCancelKb(),
                        token);
                    return;

                case ContestCreationState.WaitDescription:
                    session.Draft.Description = (text ?? "").Trim();
                    session.State = ContestCreationState.WaitMedia;

                    SaveStepToDb(session, adminId);

                    session.PanelMessageId = await _deleteAndSendHtmlAsync(
                        botClient, chatId, panelId,
                        "<b>Шаг 3/6 • Медиа</b>\n\nОтправь медиа одним сообщением (фото / GIF / видео)\nили нажми «Без медиа».",
                        _buildSkipMediaKb(),
                        token);
                    return;

                case ContestCreationState.WaitMedia:
                    string mediaType = null;
                    string fileId = null;

                    if (msg.Photo != null && msg.Photo.Length > 0)
                    {
                        mediaType = "photo";
                        fileId = msg.Photo[msg.Photo.Length - 1].FileId;
                    }
                    else if (msg.Animation != null)
                    {
                        mediaType = "animation";
                        fileId = msg.Animation.FileId;
                    }
                    else if (msg.Video != null)
                    {
                        mediaType = "video";
                        fileId = msg.Video.FileId;
                    }

                    if (string.IsNullOrEmpty(fileId))
                    {
                        session.PanelMessageId = await _deleteAndSendHtmlAsync(
                            botClient, chatId, panelId,
                            "<b>Шаг 3/6 • Медиа</b>\n\nЖду медиа: фото / GIF / видео.\n\nИли нажми «Без медиа».",
                            _buildSkipMediaKb(),
                            token);
                        return;
                    }

                    session.Draft.MediaType = mediaType;
                    session.Draft.MediaFileId = fileId;

                    if (mediaType == "photo")
                        session.Draft.ImageFileId = fileId;

                    session.State = ContestCreationState.WaitWinnersCount;

                    SaveStepToDb(session, adminId);

                    int winnersInit = ContestCreationRules.EnsureWinnersInitialized(session.Draft.WinnersCount);
                    session.Draft.WinnersCount = winnersInit;

                    session.PanelMessageId = await _deleteAndSendHtmlAsync(
                        botClient, chatId, panelId,
                        "<b>Шаг 4/6 • Призовые места</b>\n\nСколько будет призовых мест?\nМожно кнопками или отправь число сообщением.",
                        _buildWinnersCountKb(winnersInit),
                        token);
                    return;


                case ContestCreationState.WaitWinnersCount:
                    int winnersValue;
                    var winnersRes = ContestCreationStateMachine.ApplyWinnersCountText(session, text, out winnersValue);

                    if (winnersRes == ContestCreationStateMachine.WinnersApplyResult.InvalidInput)
                    {
                        session.PanelMessageId = await _deleteAndSendHtmlAsync(
                            botClient, chatId, panelId,
                            "<b>Шаг 4/6 • Призовые места</b>\n\nНужно число от 1 до 20.\n\nМожно кнопками или отправь число сообщением.",
                            _buildWinnersCountKb(winnersValue),
                            token);
                        return;
                    }

                    SaveStepToDb(session, adminId);

                    session.PanelMessageId = await _deleteAndSendHtmlAsync(
                        botClient, chatId, panelId,
                        "<b>Шаг 5/6 • Дата розыгрыша</b>\n\nВведи дату и время розыгрыша.\nФормат: <code>" + now + "</code>",
                        _buildCancelKb(),
                        token);
                    return;

                case ContestCreationState.WaitDrawDateTime:
                case ContestCreationState.EditDrawDateTime:
                    {
                        var dt = _parseDateTime((text ?? "").Trim());
                        if (!dt.HasValue)
                        {
                            session.PanelMessageId = await _deleteAndSendHtmlAsync(
                                botClient, chatId, panelId,
                                "<b>Шаг 5/6 • Дата розыгрыша</b>\n\nНе смог понять дату и время.\nФормат: <code>" + now + "</code>",
                                _buildCancelKb(),
                                token);
                            return;
                        }

                        if (dt.Value <= DateTime.Now)
                        {
                            session.PanelMessageId = await _deleteAndSendHtmlAsync(
                                botClient, chatId, panelId,
                                "<b>Шаг 5/6 • Дата розыгрыша</b>\n\nДата должна быть в будущем.\nФормат: <code>" + now + "</code>",
                                _buildCancelKb(),
                                token);
                            return;
                        }

                        session.Draft.EndAt = dt.Value;
                        session.Draft.StartAt = DateTime.Now;
                        session.Draft.Status = "Draft";

                        await ShowPreviewAsync(botClient, chatId, adminId, panelId, null, token);
                        return;
                    }

            }
        }

        public async Task ShowPreviewAsync(
             ITelegramBotClient botClient,
             long chatId,
             long adminUserId,
             int? oldMsgId,
             string header,
             CancellationToken token)
        {
            var session = _store.TryGet(adminUserId);
            if (session == null || session.Draft == null) return;

            // превью — это "как в канале", но с пометкой что это предпросмотр
            string caption = _buildContestCaption(session.Draft, 0);

            // строка канала
            string channelLine;
            if (session.Draft.ChannelId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(session.Draft.ChannelUsername))
                {
                    var uname = session.Draft.ChannelUsername.Trim();
                    if (uname.StartsWith("@")) uname = uname.Substring(1);
                    channelLine = $"Канал: <b>@{System.Net.WebUtility.HtmlEncode(uname)}</b>";
                }
                else
                {
                    channelLine = $"Канал: <b>{session.Draft.ChannelId.Value}</b>";
                }
            }
            else
            {
                channelLine = "Канал: <b>не выбран</b>";
            }

            caption = caption + "\n\n" + "<i>Предпросмотр (до публикации)</i>\n" + channelLine;

            if (!string.IsNullOrWhiteSpace(header))
                caption = System.Net.WebUtility.HtmlEncode(header) + "\n\n" + caption;

            int newId = await _deleteAndSendContestAsync(
                botClient,
                chatId,
                oldMsgId,
                session.Draft,
                caption,
                _buildPreviewKb(),
                token);

            session.PanelMessageId = newId > 0 ? (int?)newId : session.PanelMessageId;
            session.State = ContestCreationState.Preview;

            if (session.DraftId.HasValue)
                Database.UpdateContestDraft(session.DraftId.Value, adminUserId, session.State, session.Draft);
        }

        private static void SaveStepToDb(ContestCreationStore.Session session, long adminId)
        {
            if (session == null) return;
            if (!session.DraftId.HasValue) return; // до выбора типа — ничего не сохраняем
            if (session.Draft == null) return;

            Database.UpdateContestDraft(session.DraftId.Value, adminId, session.State, session.Draft);
        }
    }
}
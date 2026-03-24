using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Services;

namespace ContestBot.Admin.Manage
{
    internal sealed class AdminContestManageMessagesHandler
    {
        private readonly ContestManageStore _store;
        private readonly ContestChannelPostsService _channelPosts;
        private readonly Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task<int>> _deleteAndSendHtmlAsync;

        public AdminContestManageMessagesHandler(
            ContestManageStore store,
            ContestChannelPostsService channelPosts,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task<int>> deleteAndSendHtmlAsync)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _channelPosts = channelPosts ?? throw new ArgumentNullException(nameof(channelPosts));
            _deleteAndSendHtmlAsync = deleteAndSendHtmlAsync ?? throw new ArgumentNullException(nameof(deleteAndSendHtmlAsync));
        }

        public async Task HandleStepAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
        {
            if (bot == null || msg == null || msg.From == null) return;

            var s = _store.TryGet(msg.From.Id);
            if (s == null || !s.IsActive) return;

            text = (text ?? string.Empty).Trim();

            // не перехватываем команды
            if (!string.IsNullOrEmpty(text) && text.StartsWith("/")) return;

            // текст может быть пустым в режиме ожидания медиа
            if (string.IsNullOrEmpty(text) && s.State != ContestManageState.WaitMedia)
                return;

            var contest = Database.LoadContestById(s.ContestId.Value);
            if (contest == null)
            {
                await RenderAsync(bot, msg.Chat.Id, s, "<b>Конкурс не найден</b>",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);
                s.Reset();
                return;
            }

            if (contest.Status == "Finished")
            {
                await RenderAsync(bot, msg.Chat.Id, s,
                    "<b>Нельзя изменить завершённый конкурс</b>\n\nДля завершённого доступен только перевыбор победителей.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);
                s.Reset();
                return;
            }

            if (s.State == ContestManageState.WaitMedia)
            {
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

                if (string.IsNullOrEmpty(mediaType) || string.IsNullOrEmpty(fileId))
                {
                    await RenderAsync(
                        bot, msg.Chat.Id, s,
                        "<b>Нужно отправить фото/видео/GIF</b>",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_media_cancel:" + contest.Id) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return;
                }

                contest.MediaType = mediaType;
                contest.MediaFileId = fileId;

                if (mediaType == "photo")
                    contest.ImageFileId = fileId;

                Database.SaveContest(contest);

                var upd = await _channelPosts.RepostChannelPostWithResultAsync(bot, contest, ct);

                s.Reset();

                string note = "";
                if (upd.Attempted && upd.Success) note = "\n<i>Пост в канале обновлён.</i>";
                else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                await RenderAsync(
                    bot, msg.Chat.Id, s,
                    "<b>✅ Медиа обновлено</b>" + note,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return;
            }

            if (s.State == ContestManageState.WaitName)
            {
                contest.Name = text;
                Database.SaveContest(contest);

                var upd = await _channelPosts.TryUpdateChannelPostWithResultAsync(bot, contest, ct);

                s.Reset();

                string note = "";
                if (upd.Attempted && upd.Success) note = "\n<i>Пост в канале обновлён.</i>";
                else if (!upd.Attempted) note = "\n<i>Пост в канале не обновлён (нет id поста).</i>";
                else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                await RenderAsync(bot, msg.Chat.Id, s,
                    "<b>✅ Название обновлено</b>" + note,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return;
            }

            if (s.State == ContestManageState.WaitDescription)
            {
                contest.Description = text;
                Database.SaveContest(contest);

                var upd = await _channelPosts.TryUpdateChannelPostWithResultAsync(bot, contest, ct);

                s.Reset();

                string note = "";
                if (upd.Attempted && upd.Success) note = "\n<i>Пост в канале обновлён.</i>";
                else if (!upd.Attempted) note = "\n<i>Пост в канале не обновлён (нет id поста).</i>";
                else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                await RenderAsync(bot, msg.Chat.Id, s,
                    "<b>✅ Описание обновлено</b>" + note,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return;
            }

            if (s.State == ContestManageState.WaitDate)
            {
                if (!TryParseAdminDateTime(text, out var newDt))
                {
                    await RenderAsync(
                        bot,
                        msg.Chat.Id,
                        s,
                        "<b>Некорректная дата</b>\n\nИспользуй формат:\n<code>31.12.2026 18:30</code>\nили\n<code>31.12.2026</code>",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_date:" + s.ContestId.Value) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return;
                }

                // Если времени не было — ставим 12:00 (чтобы не было “00:00” неожиданно)
                if (newDt.TimeOfDay == TimeSpan.Zero && text.Trim().Length == "dd.MM.yyyy".Length)
                    newDt = newDt.Date.AddHours(12);

                // Запрет прошлого (строго)
                if (newDt < DateTime.Now)
                {
                    await RenderAsync(
                        bot,
                        msg.Chat.Id,
                        s,
                        "<b>Дата в прошлом</b>\n\nВыбери дату и время в будущем.",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_date:" + s.ContestId.Value) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return;
                }

                contest.EndAt = newDt;
                Database.SaveContest(contest);

                var upd = await _channelPosts.TryUpdateChannelPostWithResultAsync(bot, contest, ct);

                s.Reset();

                string note = "";
                if (upd.Attempted && upd.Success) note = "\n<i>Пост в канале обновлён.</i>";
                else if (!upd.Attempted) note = "\n<i>Пост в канале не обновлён (нет id поста).</i>";
                else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                await RenderAsync(
                    bot,
                    msg.Chat.Id,
                    s,
                    "<b>✅ Дата обновлена</b>" + note,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return;
            }

            if (s.State == ContestManageState.WaitWinnersCount)
            {
                if (!int.TryParse(text, out int cnt))
                {
                    await RenderAsync(
                        bot,
                        msg.Chat.Id,
                        s,
                        "<b>Некорректное число</b>\n\nОтправь целое число, например <code>1</code>.",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_winners:" + s.ContestId.Value) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return;
                }

                if (cnt < 1 || cnt > 50)
                {
                    await RenderAsync(
                        bot,
                        msg.Chat.Id,
                        s,
                        "<b>Недопустимое значение</b>\n\nРазрешено: от <b>1</b> до <b>50</b>.",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_winners:" + s.ContestId.Value) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return;
                }

                contest.WinnersCount = cnt;
                Database.SaveContest(contest);

                var upd = await _channelPosts.TryUpdateChannelPostWithResultAsync(bot, contest, ct);

                s.Reset();

                string note = "";
                if (upd.Attempted && upd.Success) note = "\n<i>Пост в канале обновлён.</i>";
                else if (!upd.Attempted) note = "\n<i>Пост в канале не обновлён (нет id поста).</i>";
                else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                await RenderAsync(
                    bot,
                    msg.Chat.Id,
                    s,
                    "<b>✅ Призовые места обновлены</b>" + note,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return;
            }
        }

        private static bool TryParseAdminDateTime(string input, out DateTime dt)
        {
            dt = default(DateTime);

            input = (input ?? "").Trim();
            if (string.IsNullOrEmpty(input)) return false;

            // Разрешаем: "dd.MM.yyyy HH:mm" и "dd.MM.yyyy"
            var formats = new[]
            {
                "dd.MM.yyyy HH:mm",
                "dd.MM.yyyy H:mm",
                "dd.MM.yyyy"
            };

            return DateTime.TryParseExact(
                input,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out dt);
        }

        private async Task RenderAsync(
            ITelegramBotClient bot,
            long chatId,
            ContestManageStore.Session s,
            string html,
            InlineKeyboardMarkup kb,
            CancellationToken ct)
        {
            var newId = await _deleteAndSendHtmlAsync(bot, chatId, s.PanelMessageId, html, kb, ct);
            s.PanelMessageId = newId;
        }

    }
}
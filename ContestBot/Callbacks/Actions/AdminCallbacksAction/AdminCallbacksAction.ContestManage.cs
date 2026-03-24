using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Services;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private async Task<bool> TryHandleContestManageAsync(
            ITelegramBotClient bot,
            Telegram.Bot.Types.CallbackQuery cq,
            long chatId,
            int msgId,
            CancellationToken ct)
        {
            if (cq == null || string.IsNullOrEmpty(cq.Data))
                return false;

            // admin:contest_type_ref:{id}  -> показать выбор пресета веса
            if (cq.Data.StartsWith("admin:contest_type_ref:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Реферальный конкурс</b>\n\nВыбери режим рефералов:",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Мягкая", "admin:contest_type_preset:" + contestId + ":1"),
                                InlineKeyboardButton.WithCallbackData("Стандарт", "admin:contest_type_preset:" + contestId + ":2"),
                                InlineKeyboardButton.WithCallbackData("Агрессив", "admin:contest_type_preset:" + contestId + ":3")
                            },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_type:" + contestId),
                                InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                            }
                        }),
                        ct);

                    return true;
                }

                return true;
            }
            // admin:contest_type_preset:{id}:{1|2|3} -> применить referral + веса
            if (cq.Data.StartsWith("admin:contest_type_preset:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 4 && int.TryParse(parts[2], out int contestId))
                {
                    string preset = parts[3];

                    var contest = Database.LoadContestById(contestId);
                    if (contest == null)
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Конкурс не найден</b>",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    if (contest.Status == "Finished")
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Нельзя изменить завершённый конкурс</b>\n\nДля завершённого доступен только перевыбор победителей.",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    // referral + пресет как в конструкторе
                    contest.Type = "referral";
                    contest.BaseWeight = 1;

                    if (preset == "1") { contest.PerReferralWeight = 0.2; contest.MaxWeight = 3; }
                    else if (preset == "2") { contest.PerReferralWeight = 0.3; contest.MaxWeight = 7.5; }
                    else if (preset == "3") { contest.PerReferralWeight = 0.5; contest.MaxWeight = 10; }
                    else { contest.PerReferralWeight = 0.2; contest.MaxWeight = 3; }

                    Database.SaveContest(contest);

                    bool attempted = contest.ChannelPostMessageId.HasValue && contest.ChannelPostMessageId.Value > 0;
                    bool ok = false;

                    if (attempted)
                    {
                        try { await _tryUpdateChannelPostAsync(bot, contest, ct); ok = true; }
                        catch { ok = false; }
                    }

                    string note = "";
                    if (attempted && ok) note = "\n<i>Пост в канале обновлён.</i>";
                    else if (!attempted) note = "\n<i>Пост в канале не обновлён (нет id поста).</i>";
                    else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>✅ Тип обновлён</b>" + note,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);

                    return true;
                }

                return true;
            }
            // admin:contest_type_set:{id}:{normal|referral}
            if (cq.Data.StartsWith("admin:contest_type_set:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 4 && int.TryParse(parts[2], out int contestId))
                {
                    string type = parts[3];

                    var contest = Database.LoadContestById(contestId);
                    if (contest == null)
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Конкурс не найден</b>",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    if (contest.Status == "Finished")
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Нельзя изменить завершённый конкурс</b>\n\nДля завершённого доступен только перевыбор победителей.",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    if (type != "normal" && type != "referral")
                        type = "normal";

                    // если прилетело "referral" старой кнопкой — ведём в выбор пресета
                    if (type == "referral")
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Реферальный конкурс</b>\n\nВыбери режим рефералов:",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[]
                                {
                                    InlineKeyboardButton.WithCallbackData("Мягкая", "admin:contest_type_preset:" + contestId + ":1"),
                                    InlineKeyboardButton.WithCallbackData("Стандарт", "admin:contest_type_preset:" + contestId + ":2"),
                                    InlineKeyboardButton.WithCallbackData("Агрессив", "admin:contest_type_preset:" + contestId + ":3")
                                },
                                new[]
                                {
                                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_type:" + contestId),
                                    InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                                }
                            }),
                            ct);

                        return true;
                    }

                    contest.Type = "normal";
                    contest.BaseWeight = 1;
                    contest.PerReferralWeight = 0;
                    contest.MaxWeight = 1;

                    Database.SaveContest(contest);

                    bool attempted = contest.ChannelPostMessageId.HasValue && contest.ChannelPostMessageId.Value > 0;

                    bool ok = false;
                    if (attempted)
                    {
                        try
                        {
                            await _tryUpdateChannelPostAsync(bot, contest, ct);
                            ok = true;
                        }
                        catch
                        {
                            ok = false;
                        }
                    }

                    string note = "";
                    if (attempted && ok) note = "\n<i>Пост в канале обновлён.</i>";
                    else if (!attempted) note = "\n<i>Пост в канале не обновлён (нет id поста).</i>";
                    else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>✅ Тип обновлён</b>" + note,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);

                    return true;
                }

                return true;
            }
            // admin:contest_media_del:{id} -> confirm
            if (cq.Data != null && cq.Data.StartsWith("admin:contest_media_del:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Удалить медиа?</b>\n\nПост в канале будет пересоздан как текстовый.",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Удалить", "admin:contest_media_del_do:" + contestId),
                                InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_media:" + contestId)
                            },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return true;
                }
                return true;
            }
            // admin:contest_media_del_do:{id} -> apply delete + repost
            if (cq.Data != null && cq.Data.StartsWith("admin:contest_media_del_do:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    var contest = Database.LoadContestById(contestId);
                    if (contest == null)
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Конкурс не найден</b>",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    if (contest.Status == "Finished")
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Нельзя изменить завершённый конкурс</b>\n\nДля завершённого доступен только перевыбор победителей.",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    contest.MediaType = "none";
                    contest.MediaFileId = null;
                    contest.ImageFileId = null;

                    Database.SaveContest(contest);

                    var upd = await _repostChannelPostWithResultAsync(bot, contest, ct);

                    string note = "";
                    if (upd.Attempted && upd.Success) note = "\n<i>Пост в канале обновлён.</i>";
                    else note = "\n<b>⚠️ Пост в канале не обновлён</b>";

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>✅ Медиа удалено</b>" + note,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);

                    return true;
                }
                return true;
            }
            // admin:contest_media_rep:{id} -> enter wait media mode
            if (cq.Data != null && cq.Data.StartsWith("admin:contest_media_rep:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    var contest = Database.LoadContestById(contestId);
                    if (contest == null)
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Конкурс не найден</b>",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    if (contest.Status == "Finished")
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Нельзя изменить завершённый конкурс</b>\n\nДля завершённого доступен только перевыбор победителей.",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);
                        return true;
                    }

                    var s = _manageStore.GetOrCreate(cq.From.Id);
                    s.State = ContestBot.Admin.Manage.ContestManageState.WaitMedia;
                    s.ContestId = contestId;
                    s.PanelMessageId = msgId;

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Заменить медиа</b>\n\nОтправь одним сообщением:\n— фото\n— видео\n— GIF (анимация)\n\n<i>Подпись к медиа не нужна.</i>",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_media_cancel:" + contestId) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);

                    return true;
                }

                return true;
            }

            // admin:contest_media_cancel:{id} -> reset state + back
            if (cq.Data != null && cq.Data.StartsWith("admin:contest_media_cancel:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    var s = _manageStore.GetOrCreate(cq.From.Id);
                    s.Reset();

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Отменено</b>",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contestId) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);

                    return true;
                }

                return true;
            }

            // --- CONFIRM callbacks ---
            // admin:contest_delete_confirm:{id}
            if (cq.Data.StartsWith("admin:contest_delete_confirm:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    // минимально безопасно: удаляем конкурс + победителей
                    try { Database.DeleteWinnersForContest(contestId); } catch { }
                    Database.DeleteContestById(contestId);

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Конкурс удалён</b>",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);

                    return true;
                }

                return true;
            }

            // admin:contest_finish_confirm:{id}
            if (cq.Data.StartsWith("admin:contest_finish_confirm:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    var contest = Database.LoadAllContests().FirstOrDefault(x => x.Id == contestId);
                    if (contest == null)
                    {
                        await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>Конкурс не найден</b>",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") }
                            }),
                            ct);
                        return true;
                    }

                    // твой принцип: вручную draw можно только на Finished,
                    // поэтому finish здесь только переводит в Finished.
                    contest.Status = "Finished";
                    Database.SaveContest(contest);

                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Конкурс завершён</b>\n\nТеперь можно перевыбрать победителей.",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Перевыбрать победителей", "admin_draw:" + contestId) },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contestId),
                                InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                            }
                        }),
                        ct);

                    return true;
                }

                return true;
            }

            // --- MAIN contest actions from contest card ---
            // admin:contest_xxx:{id}
            if (!cq.Data.StartsWith("admin:contest_", StringComparison.OrdinalIgnoreCase))
                return false;

            var p = cq.Data.Split(':');
            if (p.Length != 3 || !int.TryParse(p[2], out int id))
                return false;

            string action = p[1]; // contest_name / contest_desc / contest_type / contest_media / contest_date / contest_winners / contest_delete / contest_finish

            if (string.Equals(action, "contest_name", StringComparison.OrdinalIgnoreCase))
            {
                var s = _manageStore.GetOrCreate(cq.From.Id);
                s.State = ContestBot.Admin.Manage.ContestManageState.WaitName;
                s.ContestId = id;
                s.PanelMessageId = msgId;

                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Название</b>\n\nОтправь новое название одним сообщением.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            if (string.Equals(action, "contest_desc", StringComparison.OrdinalIgnoreCase))
            {
                var s = _manageStore.GetOrCreate(cq.From.Id);
                s.State = ContestBot.Admin.Manage.ContestManageState.WaitDescription;
                s.ContestId = id;
                s.PanelMessageId = msgId;

                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Описание</b>\n\nОтправь новый текст описания одним сообщением.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            if (string.Equals(action, "contest_type", StringComparison.OrdinalIgnoreCase))
            {
                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Тип конкурса</b>\n\nВыбери тип:",
                    new InlineKeyboardMarkup(new[]
                    {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Обычный", "admin:contest_type_set:" + id + ":normal"),
                        InlineKeyboardButton.WithCallbackData("Реферальный", "admin:contest_type_ref:" + id)
                    },
                    new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id) },
                    new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            if (string.Equals(action, "contest_media", StringComparison.OrdinalIgnoreCase))
            {
                var contest = Database.LoadContestById(id);
                if (contest == null)
                {
                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Конкурс не найден</b>",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return true;
                }

                if (contest.Status == "Finished")
                {
                    await EditHtmlAsync(
                        bot, chatId, msgId,
                        "<b>Нельзя изменить завершённый конкурс</b>\n\nДля завершённого доступен только перевыбор победителей.",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                            new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                        }),
                        ct);
                    return true;
                }

                string cur =
                    string.IsNullOrWhiteSpace(contest.MediaType) || contest.MediaType == "none" || string.IsNullOrWhiteSpace(contest.MediaFileId)
                        ? "<i>не выбрано</i>"
                        : "<code>" + EscapeHtml(contest.MediaType) + "</code>";

                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Медиа</b>\n\nТекущее: " + cur,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Удалить медиа", "admin:contest_media_del:" + contest.Id),
                            InlineKeyboardButton.WithCallbackData("Заменить медиа", "admin:contest_media_rep:" + contest.Id)
                        },
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + contest.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            if (string.Equals(action, "contest_date", StringComparison.OrdinalIgnoreCase))
            {
                var s = _manageStore.GetOrCreate(cq.From.Id);
                s.State = ContestBot.Admin.Manage.ContestManageState.WaitDate;
                s.ContestId = id;
                s.PanelMessageId = msgId;

                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Дата розыгрыша</b>\n\nОтправь дату и время одним сообщением.\n\n" +
                    "формат: <code>31.12.2026 18:30</code>\n" +
                    "или: <code>31.12.2026</code>",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            if (string.Equals(action, "contest_winners", StringComparison.OrdinalIgnoreCase))
            {
                var s = _manageStore.GetOrCreate(cq.From.Id);
                s.State = ContestBot.Admin.Manage.ContestManageState.WaitWinnersCount;
                s.ContestId = id;
                s.PanelMessageId = msgId;

                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Призовые места</b>\n\nОтправь число победителей одним сообщением.\n\nнапример: <code>1</code>",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            // Реальная логика: удаление
            if (string.Equals(action, "contest_delete", StringComparison.OrdinalIgnoreCase))
            {
                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Удалить конкурс?</b>\n\nЭто действие нельзя отменить.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Удалить", "admin:contest_delete_confirm:" + id),
                            InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id)
                        },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            // Реальная логика: завершение (без draw)
            if (string.Equals(action, "contest_finish", StringComparison.OrdinalIgnoreCase))
            {
                await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>Завершить конкурс?</b>\n\nПосле завершения редактирование параметров будет недоступно.\nПобедителей можно будет перевыбрать.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Завершить", "admin:contest_finish_confirm:" + id),
                            InlineKeyboardButton.WithCallbackData("Отмена", "admin:contest_open:" + id)
                        },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    ct);

                return true;
            }

            // Если вдруг прилетит неизвестное действие — не “проваливаемся”
            await EditHtmlAsync(
                bot, chatId, msgId,
                "<b>🛠 В разработке</b>",
                new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + id) },
                    new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                }),
                ct);

            return true;
        }
    }
}
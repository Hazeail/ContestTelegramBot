using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using System;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using ContestBot.Admin.Creation;

namespace ContestBot.Callbacks.Actions
{
    internal sealed partial class AdminCallbacksAction
    {
        private async Task<bool> TryHandleMenuAsync(
            ITelegramBotClient bot,
            Telegram.Bot.Types.CallbackQuery cq,
            long chatId,
            int msgId,
            CancellationToken ct)
        {
            long adminId = cq.From != null ? cq.From.Id : 0;
            var session = _creationStore.GetOrCreate(adminId);

            if (cq.Data == "admin:menu")
            {
                // Сохраняем якорь панели, но сбрасываем состояние конструктора
                int panelId = msgId;
                session.Reset();
                session.PanelMessageId = panelId;

                await _showAdminMenuAsync(bot, chatId, msgId, ct);
                return true;
            }

            if (cq.Data == "admin:close")
            {
                session.PanelMessageId = null;

                if (msgId != 0)
                    await bot.EditMessageText(
                     chatId,
                     msgId,
                     "<b>Админ-панель закрыта.</b>",
                     parseMode: ParseMode.Html,
                     cancellationToken: ct);

                return true;
            }

            if (cq.Data == "admin:home_create")
            {
                var text =
                    "<b>🧩 Создание</b>\n\n" +
                    "• <b>Новый конкурс</b> — запустить конструктор\n" +
                    "• <b>Черновики</b> — открыть сохранённые заготовки";

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("➕ Новый конкурс", "admin:create") },
                    new[] { InlineKeyboardButton.WithCallbackData("🗂 Черновики", "admin:drafts") },
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:menu") }
                });

                session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, text, kb, ct);
                return true;
            }

            if (cq.Data == "admin:home_contests")
            {
                await ShowContestsHomeAsync(bot, chatId, msgId, session, ct);
                return true;
            }

            if (cq.Data == "admin:home_manage")
            {
                var text =
                    "<b>⚙️ Управление</b>\n\n" +
                    "• <b>Админы</b> — добавить или отключить\n" +
                    "• <b>Каналы</b> — добавить или отключить\n\n" +
                    "<b>Доступ:</b> только <u>главный админ</u>";

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("👥 Админы", "admin:admins") },
                    new[] { InlineKeyboardButton.WithCallbackData("📣 Каналы", "admin:channels") },
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:menu") }
                });

                session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, text, kb, ct);
                return true;
            }

            if (cq.Data == "admin:drafts")
            {
                var drafts = Database.ListContestDrafts(adminId, onlyActive: true);

                if (drafts == null || drafts.Count == 0)
                {
                    var text = "<b>🗂 Черновики</b>\n\n<u>Черновиков нет</u>";

                    var kb = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("➕ Новый конкурс", "admin:create") },
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:home_create") }
                    });

                    session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, text, kb, ct);
                    return true;
                }

                var rows = new List<InlineKeyboardButton[]>();

                foreach (var d in drafts)
                {
                    string name = (d.Name ?? "").Trim();
                    if (string.IsNullOrEmpty(name)) name = "(без названия)";

                    // чуть укоротим, чтобы кнопки не были огромными
                    if (name.Length > 30) name = name.Substring(0, 30) + "…";

                    rows.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("📝 " + name, "admin:draft_open:" + d.DraftId)
                    });

                }

                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:home_create")
                });

                session.PanelMessageId = await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>🗂 Черновики</b>\n\nВыбери черновик:",
                    new InlineKeyboardMarkup(rows),
                    ct);

                return true;
            }

            if (cq.Data == "admin:contests_list")
            {
                await ShowContestsListAsync(bot, chatId, msgId, session, ct);
                return true;
            }

            if (cq.Data != null && cq.Data.StartsWith("admin:contest_open:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cq.Data.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int contestId))
                {
                    Contest contest = Database.LoadContestById(contestId);

                    if (contest == null)
                    {
                        session.PanelMessageId = await EditHtmlAsync(
                            bot, chatId, msgId,
                            "<b>🏆 Конкурс</b>\n\n<i>Конкурс не найден.</i>",
                            new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                            }),
                            ct);

                        return true;
                    }

                    int participantsCount = Database.CountParticipantsForContestId(contest.Id);
                    int winnersCount = Database.CountWinnersForContestId(contest.Id);

                    string channelText =
                        (!string.IsNullOrWhiteSpace(contest.ChannelUsername))
                            ? "<code>@" + EscapeHtml(contest.ChannelUsername.Trim()) + "</code>"
                            : "<i>не выбран</i>";

                    string nameText = EscapeHtml((contest.Name ?? "").Trim());
                    if (string.IsNullOrEmpty(nameText)) nameText = "(без названия)";

                    string dateText = contest.EndAt.ToString("dd.MM.yyyy HH:mm");

                    string statusText = (contest.Status == "Running") ? "🟢 Активный"
                                     : (contest.Status == "Finished") ? "⚪️ Завершённый"
                                     : EscapeHtml(contest.Status ?? "");

                    string winnersText = (winnersCount == 0)
                        ? "<i>ещё не выбраны</i>"
                        : "<code>" + winnersCount + "</code>";

                    string text =
                        "<b>🏆 Конкурс <code>#" + contest.Id + "</code></b>\n\n" +
                        "<b>Название:</b> " + nameText + "\n" +
                        "<b>Статус:</b> " + statusText + "\n" +
                        "<b>Канал:</b> " + channelText + "\n" +
                        "<b>Дата розыгрыша:</b> <code>" + dateText + "</code>\n" +
                        "<b>Участники:</b> <code>" + participantsCount + "</code>\n" +
                        "<b>Победители:</b> " + winnersText;

                    InlineKeyboardMarkup kb;

                    // FINISHED: только перевыбор победителей
                    if (contest.Status == "Finished")
                    {
                        kb = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Перевыбрать победителей", "admin_draw:" + contest.Id) },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list"),
                                InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                            }
                        });
                    }
                    else
                    {
                        // RUNNING: кнопки “изменить всё” (пока без логики, только колбеки)
                        kb = new InlineKeyboardMarkup(new[]
                        {
                            // Блок параметров
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Название", "admin:contest_name:" + contest.Id),
                                InlineKeyboardButton.WithCallbackData("Описание", "admin:contest_desc:" + contest.Id)
                            },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Тип", "admin:contest_type:" + contest.Id),
                                InlineKeyboardButton.WithCallbackData("Медиа", "admin:contest_media:" + contest.Id)
                            },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Дата розыгрыша", "admin:contest_date:" + contest.Id)
                            },

                            // /канал/прочее
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Призовые места", "admin:contest_winners:" + contest.Id),
                                InlineKeyboardButton.WithCallbackData("Удалить конкурс", "admin:contest_delete:" + contest.Id)
                            },
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("Завершить", "admin:contest_finish:" + contest.Id)
                            },

                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list"),
                                InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                            }
                        });
                    }

                    session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, text, kb, ct);
                    return true;
                }

                // если криво распарсили — мягкий фолбэк
                session.PanelMessageId = await EditHtmlAsync(
                    bot, chatId, msgId,
                    "<b>🏆 Конкурс</b>\n\n<i>Не понял конкурс. Открой список ещё раз.</i>",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") }
                    }),
                    ct);

                return true;
            }

            if (cq.Data != null && cq.Data.StartsWith("admin:contest_", StringComparison.OrdinalIgnoreCase))
            {
                if (await TryHandleContestManageAsync(bot, cq, chatId, msgId, ct))
                    return true;

                return true;
            }

            if (cq.Data != null && cq.Data.StartsWith("admin:draft_open:", StringComparison.OrdinalIgnoreCase))
            {
                // admin:draft_open:{draftId}
                var parts = cq.Data.Split(':');
                if (parts.Length == 3)
                {
                    long draftId;
                    if (long.TryParse(parts[2], out draftId))
                    {
                        var row = Database.LoadContestDraft(adminId, draftId);
                        if (row == null || row.Draft == null)
                        {
                            session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, "Черновик не найден или недоступен.", _buildAdminMenuKb(), ct);

                            return true;
                        }

                        session.DraftId = row.DraftId;
                        session.Draft = row.Draft;
                        session.State = row.State;

                        // покажем превью (оно же “центр управления” редактированием)
                        await _showCreationPreviewAsync(bot, chatId, adminId, msgId, "Черновик загружен", ct);
                        return true;
                    }
                }

                session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, "Не понял черновик. Открой список ещё раз.", _buildAdminMenuKb(), ct);

                return true;
            }

            return false;
        }

        private async Task ShowContestsHomeAsync(
            ITelegramBotClient bot,
            long chatId,
            int msgId,
            ContestCreationStore.Session session,
            CancellationToken ct)
        {
            var text =
                "<b>🏆 Конкурсы</b>\n\n" +
                "Открой конкурс из списка — появится карточка с изменениями.";

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📋 Список конкурсов", "admin:contests_list") },
                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:menu") }
            });

            session.PanelMessageId = await EditHtmlAsync(bot, chatId, msgId, text, kb, ct);
        }

        private async Task ShowContestsListAsync(
            ITelegramBotClient bot,
            long chatId,
            int msgId,
            ContestCreationStore.Session session,
            CancellationToken ct)
        {
            // для списка достаточно Id/Status/Name — не тянем весь Contest
            var contests = Database.LoadContestsForAdminList();

            var rows = new List<InlineKeyboardButton[]>();

            foreach (var c in contests)
            {
                if (c == null) continue;
                if (c.Status != "Running" && c.Status != "Finished") continue;

                string mark = (c.Status == "Running") ? "🟢" : "⚪️";

                string name = (c.Name ?? "").Trim();
                if (string.IsNullOrEmpty(name)) name = "(без названия)";
                if (name.Length > 30) name = name.Substring(0, 30) + "…";

                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{mark} #{c.Id} · {name}", "admin:contest_open:" + c.Id)
                });
            }

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:home_contests")
            });

            session.PanelMessageId = await EditHtmlAsync(
                bot, chatId, msgId,
                "<b>📋 Список конкурсов</b>\n\n<code>🟢  -  Активный</code> \n<code>⚪️  -  Завершенный</code>",
                new InlineKeyboardMarkup(rows),
                ct);

        }

        private static string EscapeHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static bool IsIgnorableEditError(ApiRequestException ex)
        {
            if (ex == null) return false;
            var msg = (ex.Message ?? "").ToLowerInvariant();
            return msg.Contains("message is not modified");
        }

        private static async Task<int> EditHtmlAsync(
            ITelegramBotClient bot,
            long chatId,
            int msgId,
            string html,
            InlineKeyboardMarkup kb,
            CancellationToken ct)
        {
            // если якоря нет — просто отправляем новое сообщение
            if (msgId == 0)
            {
                var sent0 = await bot.SendMessage(
                    chatId,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: kb,
                    cancellationToken: ct);

                return sent0.MessageId;
            }

            try
            {
                await bot.EditMessageText(
                    chatId,
                    msgId,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: kb,
                    cancellationToken: ct);

                return msgId;
            }
            catch (ApiRequestException ex) when (IsIgnorableEditError(ex))
            {
                // Telegram: message is not modified — это ок
                return msgId;
            }
            catch (ApiRequestException)
            {
                // если сообщение не редактируется — шлём новое (НЕ delete+send)
                var sent = await bot.SendMessage(
                    chatId,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: kb,
                    cancellationToken: ct);

                return sent.MessageId;
            }
        }

    }
}
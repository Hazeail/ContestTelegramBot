using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ContestBot.Services.Draw;

namespace ContestBot.Admin.Draw
{
    internal sealed class AdminDrawHandler
    {
        private readonly ContestManager _contestManager;
        private readonly Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> _sendOrEditHtmlAsync;
        private readonly Func<ITelegramBotClient, CancellationToken, Task<ContestBot.Services.ContestDrawService.DrawOutcome>> _doDrawAsync;
        private readonly ContestBot.Services.OwnerNotificationService _ownerNotify;

        public AdminDrawHandler(
            ContestManager contestManager,
            Func<ITelegramBotClient, long, int?, string, InlineKeyboardMarkup, CancellationToken, Task> sendOrEditHtmlAsync,
            Func<ITelegramBotClient, CancellationToken, Task<ContestBot.Services.ContestDrawService.DrawOutcome>> doDrawAsync,
            long superAdminUserId,
            Func<long, bool> isAdmin)
        {
            _contestManager = contestManager;
            _sendOrEditHtmlAsync = sendOrEditHtmlAsync;
            _doDrawAsync = doDrawAsync;

            _ownerNotify = new ContestBot.Services.OwnerNotificationService(
                (b, id, msg, ct) => b.SendMessage(
                    id,
                    msg,
                    parseMode: ParseMode.Html,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: ct
                ),
                superAdminUserId,
                isAdmin
            );
        }

        public async Task RunAdminDrawAsync(
            ITelegramBotClient botClient,
            long chatId,
            int contestId,
            int? editMessageId,
            CancellationToken token)
        {

            var selected = Database.LoadContestById(contestId);
            if (selected == null)
            {
                await _sendOrEditHtmlAsync(
                    botClient,
                    chatId,
                    editMessageId,
                    "Конкурс не найден.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contests_list") },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    token
                );
                return;
            }

            DrawMode mode;
            if (!DrawRules.TryGetAdminDrawMode(selected, DateTime.Now, out mode))
            {
                await _sendOrEditHtmlAsync(
                    botClient,
                    chatId,
                    editMessageId,
                    "Сейчас перевыбор победителей доступен только для завершённого конкурса.",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + selected.Id) },
                        new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
                    }),
                    token
                );
                return;
            }

            if (DateTime.Now < selected.StartAt) return;

            // Разрешаем:
            // - Running (даже если EndAt уже прошёл — “поздний розыгрыш”)
            // - Finished (это и есть “перевыбрать победителей”)
            if (selected.Status != "Running" && selected.Status != "Finished")
                return;

            var prev = _contestManager.GetCurrentContest();
            _contestManager.SetContest(selected);

            try
            {
                var outcome = await _doDrawAsync(botClient, token);
                var winners = outcome != null ? outcome.Winners : null;

                if (outcome?.PostUpdate != null && outcome.PostUpdate.Attempted && !outcome.PostUpdate.Success)
                {
                    // уведомляем только владельца конкурса (CreatedByAdminUserId), с fallback супер-админу
                    string channelPart = outcome.PostUpdate.TargetChannelId != 0 ? outcome.PostUpdate.TargetChannelId.ToString() : "неизвестно";

                    string safeName = WebUtility.HtmlEncode(selected.Name ?? "");
                    string safeChannel = WebUtility.HtmlEncode(channelPart);
                    string safeErr = WebUtility.HtmlEncode(outcome.PostUpdate.Error ?? "ошибка Telegram");

                    string msg =
                        "<b>⚠️ Не удалось обновить пост в канале</b>\n" +
                        "Конкурс: <code>#" + selected.Id + "</code> — <b>" + safeName + "</b>\n" +
                        "Канал: <code>" + safeChannel + "</code>\n" +
                        "Причина: <i>" + safeErr + "</i>\n\n" +
                        "<b>Действие:</b> открой конкурс в админке и нажми «Перевыбрать» ещё раз.";

                    await _ownerNotify.NotifyOwnerAsync(botClient, chatId, selected, actor: null, text: msg, token: token);
                }

                // Отправляем итог инициатору (chatId)
                string resultText;

                if (winners == null || winners.Count == 0)
                {
                    resultText = "<b>✅ Победители выбраны</b>\n\nПобедителей нет (не было участников).";
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<b>✅ Победители выбраны</b>");
                    sb.AppendLine();

                    for (int i = 0; i < winners.Count; i++)
                    {
                        var w = winners[i];

                        string display = w.GetDisplayNameWithoutId(); // "@username" или "Имя Фамилия" или null

                        string line;
                        if (!string.IsNullOrWhiteSpace(display) && display.StartsWith("@"))
                        {
                            // без <code>
                            line = WebUtility.HtmlEncode(display);
                        }
                        else if (!string.IsNullOrWhiteSpace(display))
                        {
                            // имя как ссылка на профиль
                            line = "<a href=\"tg://user?id=" + w.UserId + "\">" + WebUtility.HtmlEncode(display) + "</a>";
                        }
                        else
                        {
                            // fallback, если вообще нечего показать
                            line = "<a href=\"tg://user?id=" + w.UserId + "\">Открыть профиль</a>";
                        }

                        sb.AppendLine((i + 1) + ") " + line);
                    }

                    resultText = sb.ToString();
                }

                // редактируем ТО ЖЕ сообщение (обычно это карточка конкурса), без спама в чат
                await _sendOrEditHtmlAsync(
                    botClient,
                    chatId,
                    editMessageId,
                    resultText,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:contest_open:" + selected.Id),
                            InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                        }
                    }),
                    token
                );
            }
            finally
            {
                if (prev != null)
                    _contestManager.SetContest(prev);
            }
        }
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContestBot.Services
{
    internal sealed class ChannelPostUpdateResult
    {
        public bool Attempted { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public long TargetChannelId { get; set; }
        public int? TargetMessageId { get; set; }
    }
    internal sealed class ContestChannelPostsService
    {
        private readonly long _channelId;
        private readonly string _botUsername;
        private readonly ContestManager _contestManager;

        public ContestChannelPostsService(long channelId, string botUsername, ContestManager contestManager)
        {
            _channelId = channelId;
            _botUsername = NormalizeBotUsername(botUsername);
            _contestManager = contestManager;
        }

        public string BuildContestCaption(Contest contest, int participantsCount)
        {
            if (contest == null) return string.Empty;

            var now = DateTime.Now;
            var diff = contest.EndAt - now;
            // Если до конца меньше суток, но конкурс ещё идёт — показываем 1 день вместо 0.
            int daysLeft = diff.TotalDays <= 0 ? 0 : Math.Max(1, (int)Math.Floor(diff.TotalDays));

            string datePart = contest.EndAt.ToString("HH:mm, dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
            string daysPart = daysLeft == 1 ? "1 день" : (daysLeft >= 2 && daysLeft <= 4 ? daysLeft + " дня" : daysLeft + " дней");

            string name = System.Net.WebUtility.HtmlEncode((contest.Name ?? "").Trim());
            string desc = System.Net.WebUtility.HtmlEncode((contest.Description ?? "").Trim());

            return
                 "<b>" + name + "</b>\n\n" +
                 desc + "\n\n" +
                 $"Участников: <b>{participantsCount}</b>\n" +
                 $"Призовых мест: <b>{contest.WinnersCount}</b>\n" +
                 $"Дата розыгрыша: <b>{datePart} ({daysPart})</b>";
        }

        private string AppendWinnersBlockIfFinished(Contest contest, string baseText)
        {
            if (contest == null) return baseText;

            bool finished = string.Equals(contest.Status, "Finished", StringComparison.OrdinalIgnoreCase);
            if (!finished) return baseText;

            var winners = Database.LoadWinnersForContest(contest);

            var sb = new System.Text.StringBuilder();
            sb.Append(baseText);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("<b>Результаты</b>");

            if (winners == null || winners.Count == 0)
            {
                sb.AppendLine("Участников не было — победителей нет.");
                return sb.ToString();
            }

            for (int i = 0; i < winners.Count; i++)
            {
                var w = winners[i];
                string display = w.GetDisplayNameWithoutId();

                string winnerHtml;
                if (!string.IsNullOrWhiteSpace(display) && display.StartsWith("@"))
                {
                    winnerHtml = System.Net.WebUtility.HtmlEncode(display);
                }
                else if (!string.IsNullOrWhiteSpace(display))
                {
                    winnerHtml = "<a href=\"tg://user?id=" + w.UserId + "\">" + System.Net.WebUtility.HtmlEncode(display) + "</a>";
                }
                else
                {
                    winnerHtml = "<a href=\"tg://user?id=" + w.UserId + "\">Открыть профиль</a>";
                }

                sb.AppendLine((i + 1) + ". " + winnerHtml);
            }

            return sb.ToString();
        }

        public InlineKeyboardMarkup BuildJoinKeyboard(Contest contest)
        {
            string payload = contest != null ? ("cid_" + contest.Id) : string.Empty;
            string username = NormalizeBotUsername(_botUsername);
            string url = string.IsNullOrWhiteSpace(username)
                ? "https://t.me" // fallback: не будет "несуществующего пользователя"
                : $"https://t.me/{username}?start={payload}";

            bool finished = contest != null &&
                string.Equals(contest.Status, "Finished", StringComparison.OrdinalIgnoreCase);

            string buttonText = finished ? "Результаты конкурса" : "Участвовать";

            return new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithUrl(buttonText, url) }
            });
        }

        public async Task PublishContestToChannelAsync(ITelegramBotClient botClient, Contest contest, CancellationToken token)
        {
            long targetChannelId = contest.ChannelId.HasValue ? contest.ChannelId.Value : _channelId;
            contest.ChannelId = targetChannelId; // чтобы сохранилось в БД даже если пришли старым путём

            if (contest == null) return;

            int participantsCount = 0;
            string caption = BuildContestCaption(contest, participantsCount);
            var kb = BuildJoinKeyboard(contest);

            Message sent = null;

            string mediaType = (contest.MediaType ?? "").ToLowerInvariant();
            string fileId = contest.MediaFileId;

            if (string.IsNullOrEmpty(mediaType) || mediaType == "none" || string.IsNullOrEmpty(fileId))
            {
                sent = await botClient.SendMessage(targetChannelId, caption, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: token);
            }
            else if (mediaType == "photo")
            {
                sent = await botClient.SendPhoto(targetChannelId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: token);
            }
            else if (mediaType == "animation")
            {
                sent = await botClient.SendAnimation(targetChannelId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: token);
            }
            else if (mediaType == "video")
            {
                sent = await botClient.SendVideo(targetChannelId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: token);
            }
            else
            {
                sent = await botClient.SendMessage(targetChannelId, caption, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: token);
            }

            contest.ChannelPostMessageId = sent != null ? (int?)sent.MessageId : null;
            contest.Status = "Running";
            contest.StartAt = DateTime.Now;

            if (string.IsNullOrEmpty(contest.ImageFileId) && mediaType == "photo")
                contest.ImageFileId = fileId;

            _contestManager.SetContest(contest);
            Database.SaveContest(contest);
        }

        public async Task TryUpdateChannelPostAsync(ITelegramBotClient botClient, Contest contest, CancellationToken token)
        {
            await TryUpdateChannelPostWithResultAsync(botClient, contest, token);
        }

        public async Task<ChannelPostUpdateResult> TryUpdateChannelPostWithResultAsync(
            ITelegramBotClient botClient,
            Contest contest,
            CancellationToken token)
        {
            var res = new ChannelPostUpdateResult();

            if (contest == null)
            {
                res.Attempted = false;
                res.Success = false;
                res.Error = "contest == null";
                return res;
            }

            long targetChannelId = contest.ChannelId.HasValue ? contest.ChannelId.Value : _channelId;
            res.TargetChannelId = targetChannelId;
            res.TargetMessageId = contest.ChannelPostMessageId;

            // освежаем из БД (как у тебя было)
            var dbContest = Database.LoadAllContests().FirstOrDefault(c => c.Id == contest.Id);
            if (dbContest != null) contest = dbContest;

            if (!contest.ChannelPostMessageId.HasValue)
            {
                res.Attempted = false;
                res.Success = true; // нечего обновлять — не считаем ошибкой
                return res;
            }

            res.Attempted = true;

            var participants = Database.LoadParticipantsForContest(contest);
            string captionOrText = BuildContestCaption(contest, participants.Count);
            captionOrText = AppendWinnersBlockIfFinished(contest, captionOrText);
            var kb = BuildJoinKeyboard(contest);

            try
            {
                string mediaType = (contest.MediaType ?? "").ToLowerInvariant();
                string fileId = contest.MediaFileId;

                bool isMedia =
                    !string.IsNullOrEmpty(mediaType) &&
                    mediaType != "none" &&
                    !string.IsNullOrEmpty(fileId) &&
                    (mediaType == "photo" || mediaType == "video" || mediaType == "animation");

                if (isMedia)
                {
                    try
                    {
                        await botClient.EditMessageCaption(
                            targetChannelId,
                            contest.ChannelPostMessageId.Value,
                            captionOrText,
                            parseMode: ParseMode.Html,
                            replyMarkup: kb,
                            cancellationToken: token);
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                        when (ex.ErrorCode == 400 && ex.Message != null && ex.Message.Contains("message is not modified"))
                    {
                        res.Success = true;
                        return res;
                    }
                }
                else
                {
                    try
                    {
                        await botClient.EditMessageText(
                            targetChannelId,
                            contest.ChannelPostMessageId.Value,
                            captionOrText,
                            parseMode: ParseMode.Html,
                            replyMarkup: kb,
                            cancellationToken: token);
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                        when (ex.ErrorCode == 400 && ex.Message != null && ex.Message.Contains("message is not modified"))
                    {
                        res.Success = true;
                        return res;
                    }
                }

                res.Success = true;
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Error = ex.Message ?? ex.ToString();
                return res;
            }
        }

        public async Task<ChannelPostUpdateResult> RepostChannelPostWithResultAsync(
            ITelegramBotClient botClient,
            Contest contest,
            CancellationToken token)
        {
            var res = new ChannelPostUpdateResult();

            if (contest == null)
            {
                res.Attempted = false;
                res.Success = false;
                res.Error = "contest == null";
                return res;
            }

            long targetChannelId = contest.ChannelId.HasValue ? contest.ChannelId.Value : _channelId;
            res.TargetChannelId = targetChannelId;
            res.TargetMessageId = contest.ChannelPostMessageId;

            // освежаем из БД
            var dbContest = Database.LoadContestById(contest.Id);
            if (dbContest != null) contest = dbContest;

            res.Attempted = true;

            // 1) удаляем старый пост (если есть)
            if (contest.ChannelPostMessageId.HasValue)
            {
                try
                {
                    await botClient.DeleteMessage(targetChannelId, contest.ChannelPostMessageId.Value, token);
                }
                catch
                {
                    // если не удалилось — всё равно попробуем отправить новый
                }
            }

            // 2) собираем текст + клавиатуру
            var participants = Database.LoadParticipantsForContest(contest);
            string captionOrText = BuildContestCaption(contest, participants.Count);
            captionOrText = AppendWinnersBlockIfFinished(contest, captionOrText);
            var kb = BuildJoinKeyboard(contest);

            // 3) отправляем новый пост с текущим mediaType/fileId
            try
            {
                var sent = await SendContestMessageAsync(
                    botClient,
                    targetChannelId,
                    contest,
                    captionOrText,
                    kb,
                    token);

                contest.ChannelPostMessageId = sent != null ? (int?)sent.MessageId : null;

                // синхроним кеш и БД
                _contestManager.SetContest(contest);
                Database.SaveContest(contest);

                res.Success = true;
                res.TargetMessageId = contest.ChannelPostMessageId;
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Error = ex.Message ?? ex.ToString();
                return res;
            }
        }

        public async Task<Message> SendContestMessageAsync(
            ITelegramBotClient botClient,
            long chatId,
            Contest contest,
            string caption,
            InlineKeyboardMarkup replyMarkup,
            CancellationToken token)
        {
            string mediaType = (contest != null ? (contest.MediaType ?? "") : "").ToLowerInvariant();
            string fileId = contest != null ? contest.MediaFileId : null;

            if (string.IsNullOrEmpty(mediaType) || mediaType == "none" || string.IsNullOrEmpty(fileId))
                return await botClient.SendMessage(chatId, caption, parseMode: ParseMode.Html, replyMarkup: replyMarkup, cancellationToken: token);

            if (mediaType == "photo")
                return await botClient.SendPhoto(chatId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: replyMarkup, cancellationToken: token);

            if (mediaType == "animation")
                return await botClient.SendAnimation(chatId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: replyMarkup, cancellationToken: token);

            if (mediaType == "video")
                return await botClient.SendVideo(chatId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: replyMarkup, cancellationToken: token);

            return await botClient.SendMessage(chatId, caption, parseMode: ParseMode.Html, replyMarkup: replyMarkup, cancellationToken: token);
        }

        private static string NormalizeBotUsername(string botUsername)
        {
            string u = (botUsername ?? string.Empty).Trim();
            if (u.StartsWith("@")) u = u.Substring(1);
            return u;
        }
    }
}

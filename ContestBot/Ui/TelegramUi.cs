using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;

namespace ContestBot.Ui
{
    internal sealed class TelegramUi
    {
        private readonly Services.ContestChannelPostsService _channelPosts;

        public TelegramUi(Services.ContestChannelPostsService channelPosts)
        {
            _channelPosts = channelPosts;
        }

        public async Task SendOrEditHtmlAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? editMessageId,
            string html,
            InlineKeyboardMarkup replyMarkup,
            CancellationToken token)
        {
            if (!editMessageId.HasValue)
            {
                await botClient.SendMessage(
                    chatId,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: replyMarkup,
                    cancellationToken: token
                );
                return;
            }

            try
            {
                await botClient.EditMessageText(
                    chatId,
                    editMessageId.Value,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: replyMarkup,
                    cancellationToken: token
                );
            }
            catch (ApiRequestException ex) when (IsIgnorableEditError(ex))
            {
                // Telegram: message is not modified — это ОК, ничего не делаем
                return;
            }
            catch (ApiRequestException)
            {
                // если сообщение не редактируется — отправляем новое
                await botClient.SendMessage(
                    chatId,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: replyMarkup,
                    cancellationToken: token
                );
            }
            catch
            {
                // на всякий случай — тоже отправляем новое
                await botClient.SendMessage(
                    chatId,
                    html,
                    parseMode: ParseMode.Html,
                    replyMarkup: replyMarkup,
                    cancellationToken: token
                );
            }
        }

        private static bool IsIgnorableEditError(ApiRequestException ex)
        {
            if (ex == null) return false;
            var msg = (ex.Message ?? "").ToLowerInvariant();
            return msg.Contains("message is not modified");
        }

        public async Task<int> DeleteAndSendHtmlAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? oldMessageId,
            string html,
            InlineKeyboardMarkup replyMarkup,
            CancellationToken token)
        {
            if (oldMessageId.HasValue)
            {
                try { await botClient.DeleteMessage(chatId, oldMessageId.Value, cancellationToken: token); }
                catch { }
            }

            var sent = await botClient.SendMessage(
                chatId,
                html,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: token);

            return sent.MessageId;
        }

        public async Task<int> DeleteAndSendContestAsync(
            ITelegramBotClient botClient,
            long chatId,
            int? oldMessageId,
            Contest contest,
            string caption,
            InlineKeyboardMarkup replyMarkup,
            CancellationToken token)
        {
            if (oldMessageId.HasValue)
            {
                try { await botClient.DeleteMessage(chatId, oldMessageId.Value, cancellationToken: token); }
                catch { }
            }

            var sent = await _channelPosts.SendContestMessageAsync(botClient, chatId, contest, caption, replyMarkup, token);
            return sent != null ? sent.MessageId : 0;
        }
    }
}

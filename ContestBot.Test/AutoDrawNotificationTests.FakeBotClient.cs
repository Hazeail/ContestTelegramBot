using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ContestBot.Test
{
    public partial class AutoDrawNotificationTests
    {
        private sealed class FakeBotClient : ITelegramBotClient
        {
            private readonly Func<long, string, Task> _onSend;

            public FakeBotClient(Func<long, string, Task> onSend)
            {
                _onSend = onSend;
            }

            // ---- required interface members ----
            public bool LocalBotServer => false;
            public long BotId => 0;

            public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
            public IExceptionParser ExceptionsParser { get; set; }

            // события можно оставить пустыми (в тестах не нужны)
            public event AsyncEventHandler<ApiRequestEventArgs> OnMakingApiRequest { add { } remove { } }
            public event AsyncEventHandler<ApiResponseEventArgs> OnApiResponseReceived { add { } remove { } }

            public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<bool> TestApi(CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            // ВАЖНО: SendMessage(...) extension внутри Telegram.Bot вызывает SendRequest(...)
            public async Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                if (request is SendMessageRequest smr)
                {
                    long chatId = smr.ChatId.Identifier ?? 0;
                    string text = smr.Text ?? "";

                    // ВАЖНО: если _onSend бросит — это исключение должно "вылезти"
                    // чтобы SendToOwnerWithFallbackAsync ушёл в fallback.
                    await _onSend(chatId, text);

                    if (typeof(TResponse) == typeof(Message))
                        return (TResponse)(object)new Message();

                    return default(TResponse);
                }

                return default(TResponse);
            }
        }
    }
}
using ContestBot.Config;
using ContestBot.Services;
using System;
using System.Net;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ContestBot
{
    class BotService
    {
        private readonly ITelegramBotClient _bot;
        private readonly UpdateHandler _updateHandler;
        private readonly BotSettings _settings;
        private readonly CrashLogger _log = new CrashLogger();

        private CancellationTokenSource _cts;
        private Task _timerTask;

        public BotService(BotSettings settings)
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _bot = new TelegramBotClient(_settings.Token);

            _updateHandler = new UpdateHandler(_settings);
        }


        public void Run()
        {
            _cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true; // не убиваем процесс резко
                TryStop();
            };

            var me = _bot.GetMe().GetAwaiter().GetResult();
            Console.WriteLine("Бот запущен: @" + me.Username);
            _log.Info("Лог файл: " + _log.PathToLog);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            _bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: _cts.Token
            );

            // Фоновый таймер конкурсов
            _timerTask = Task.Run(() => TimerLoopAsync(_cts.Token), _cts.Token);

            Console.WriteLine("Бот работает. Нажмите Enter, чтобы остановить...");
            Console.ReadLine();

            TryStop();

            // Дожидаемся остановки таймера (чтобы не оставлять фоновые исключения)
            try
            {
                _timerTask?.GetAwaiter().GetResult();
            }
            catch { }

            _cts.Dispose();
            _cts = null;

            Console.WriteLine("Бот остановлен.");
        }

        private void TryStop()
        {
            if (_cts == null) return;
            if (_cts.IsCancellationRequested) return;

            Console.WriteLine("Останавливаю бота...");
            _cts.Cancel();
        }

        private async Task TimerLoopAsync(CancellationToken token)
        {
            var nextHeartbeatAt = DateTime.MinValue;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_settings.TimersEnabled)
                        await _updateHandler.CheckContestTimersAsync(_bot, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[TIMER ERROR] " + ex);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                try
                {
                    var now = DateTime.Now;
                    if (now >= nextHeartbeatAt)
                    {
                        nextHeartbeatAt = now.AddMinutes(5);

                        _log.Info(
                            "TIMER TICK ok " +
                            $"TimersEnabled={_settings.TimersEnabled} " +
                            $"AutoDrawEnabled={_settings.AutoDrawEnabled} " +
                            _updateHandler.GetTimerDebugState()
                        );
                    }
                }
                catch { }
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            try
            {
                string chatId =
                (update?.Message?.Chat?.Id ?? update?.CallbackQuery?.Message?.Chat?.Id)?.ToString() ?? "-";
                string fromId =
                    (update?.Message?.From?.Id ?? update?.CallbackQuery?.From?.Id)?.ToString() ?? "-";
                string msgText = update?.Message?.Text ?? update?.Message?.Caption ?? "";
                string cbData = update?.CallbackQuery?.Data ?? "";

                _log.Info(
                    $"IN type={update?.Type} chatId={chatId} fromId={fromId} msg=\"{msgText}\" cb=\"{cbData}\""
                );

                await _updateHandler.HandleUpdateAsync(botClient, update, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // нормальная остановка
            }
            catch (Exception ex)
            {
                string chatId =
                    (update?.Message?.Chat?.Id ?? update?.CallbackQuery?.Message?.Chat?.Id)?.ToString() ?? "-";
                string fromId =
                    (update?.Message?.From?.Id ?? update?.CallbackQuery?.From?.Id)?.ToString() ?? "-";
                string msgText = update?.Message?.Text ?? update?.Message?.Caption ?? "";
                string cbData = update?.CallbackQuery?.Data ?? "";

                _log.Error(
                    $"UPDATE ERROR type={update?.Type} chatId={chatId} fromId={fromId} msg=\"{msgText}\" cb=\"{cbData}\"",
                    ex
                );
            }

        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken token)
        {
            if (exception is ApiRequestException apiEx)
            {
                _log.Error("Telegram API error: " + apiEx.Message, apiEx);
            }
            else if (exception is OperationCanceledException && token.IsCancellationRequested)
            {
                // нормальная остановка
            }
            else
            {
                _log.Error("Polling error", exception);
            }

            return Task.CompletedTask;
        }
    }
}

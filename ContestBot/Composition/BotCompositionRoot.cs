using ContestBot.Config;
using ContestBot.Services;
using ContestBot.Ui;
using ContestBot.Utils;
using ContestBot.Admin.Creation;
using System;

namespace ContestBot.Composition
{
    internal sealed class BotCompositionRoot
    {
        public BotSettings Settings { get; }

        public CrashLogger Crash { get; }
        public ContestManager ContestManager { get; }
        public ParticipantsManager Participants { get; }
        public ContestCreationStore CreationStore { get; }

        public ContestChannelPostsService ChannelPosts { get; }
        public ChannelSubscriptionService Subs { get; }

        public TelegramUi Ui { get; }
        public Keyboards Kb { get; }
        public DateTimeParser Dt { get; }

        public ContestDrawService DrawService { get; }

        public BotCompositionRoot(BotSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Crash = new CrashLogger();
            Console.WriteLine("[LOG] crash file: " + Crash.PathToLog);

            ContestManager = new ContestManager();
            Participants = new ParticipantsManager(ContestManager);

            CreationStore = new ContestCreationStore();

            // канал публикации/подписки внутри ContestChannelPostsService уже работает от Contest.ChannelId,
            // но Settings.ChannelId оставляем как дефолт/фолбэк
            ChannelPosts = new ContestChannelPostsService(Settings.ChannelId, Settings.BotUsername, ContestManager);
            Subs = new ChannelSubscriptionService();

            Ui = new TelegramUi(ChannelPosts);
            Kb = new Keyboards();
            Dt = new DateTimeParser();

            DrawService = new ContestDrawService(ContestManager, Participants, ChannelPosts);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;

namespace ContestBot.Services
{
    internal sealed class ContestDrawService
    {
        private readonly ContestManager _contestManager;
        private readonly ParticipantsManager _participantsManager;
        private readonly ContestChannelPostsService _channelPosts;

        internal sealed class DrawOutcome
        {
            public List<Participant> Winners { get; set; }
            public ChannelPostUpdateResult PostUpdate { get; set; }
        }

        public ContestDrawService(ContestManager contestManager, ParticipantsManager participantsManager, ContestChannelPostsService channelPosts)
        {
            _contestManager = contestManager;
            _participantsManager = participantsManager;
            _channelPosts = channelPosts;
        }

        public async Task<List<Participant>> DoDrawAsync(ITelegramBotClient botClient, CancellationToken token)
        {
            var outcome = await DoDrawWithOutcomeAsync(botClient, token);
            return outcome.Winners ?? new List<Participant>();
        }

        public async Task<DrawOutcome> DoDrawWithOutcomeAsync(ITelegramBotClient botClient, CancellationToken token)
        {
            var outcome = new DrawOutcome { Winners = new List<Participant>() };

            var rng = new Random();

            var contest = _contestManager.GetCurrentContest();
            int winnersCount = 1;
            if (contest != null && contest.WinnersCount > 0)
                winnersCount = contest.WinnersCount;

            var winners = _participantsManager.ChooseWinners(rng, winnersCount) ?? new List<Participant>();
            outcome.Winners = winners;

            if (contest != null)
            {
                // winners table
                try
                {
                    if (winners.Count == 0) Database.DeleteWinnersForContest(contest.Id);
                    else Database.SaveWinners(contest, winners);
                }
                catch (Exception ex) { Console.WriteLine("[DB] Error SaveWinners: " + ex.Message); }

                contest.Status = "Finished";
                Database.SaveContest(contest);

                // channel post update with result
                try
                {
                    outcome.PostUpdate = await _channelPosts.TryUpdateChannelPostWithResultAsync(botClient, contest, token);
                    if (outcome.PostUpdate != null && outcome.PostUpdate.Attempted && !outcome.PostUpdate.Success)
                        Console.WriteLine("[DRAW] Channel post update failed: " + outcome.PostUpdate.Error);
                }
                catch (Exception ex)
                {
                    outcome.PostUpdate = new ChannelPostUpdateResult
                    {
                        Attempted = true,
                        Success = false,
                        Error = ex.Message ?? ex.ToString(),
                        TargetChannelId = contest.ChannelId ?? 0,
                        TargetMessageId = contest.ChannelPostMessageId
                    };
                }
            }

            // поздравления победителям в личку
            foreach (var w in winners)
            {
                try
                {
                    string contestName = WebUtility.HtmlEncode(contest.Name ?? "");
                    string winMsg =
                        "<b>🎉 Поздравляем!</b>\n" +
                        "Ты выиграл(а) в конкурсе <b>" + contestName + "</b>.\n\n" +
                        "Результаты можно посмотреть в посте конкурса.";

                    await botClient.SendMessage(
                        w.UserId,
                        winMsg,
                        parseMode: ParseMode.Html,
                        cancellationToken: token
                    );
                }
                catch 
                {
                    Console.WriteLine("[DRAW] WINNER DID NOT RECEIVE THE MESSAGE");
                }
            }

            return outcome;
        }
    }
}

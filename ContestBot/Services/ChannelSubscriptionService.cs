using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ContestBot.Services
{
    internal sealed class ChannelSubscriptionService
    {

        // Новый метод: проверка подписки на конкретный канал конкурса
        public async Task<bool> IsUserSubscribedAsync(ITelegramBotClient botClient, long channelId, long userId, CancellationToken token)
        {
            try
            {
                var member = await botClient.GetChatMember(channelId, userId, cancellationToken: token);

                return member.Status == ChatMemberStatus.Member
                    || member.Status == ChatMemberStatus.Administrator
                    || member.Status == ChatMemberStatus.Creator;
            }
            catch
            {
                return false;
            }
        }
    }
}
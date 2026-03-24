using System;
using System.Linq;

namespace ContestBot.Services
{
    internal sealed class ReferralService
    {
        private readonly Func<string, Contest> _getContestByCode;
        private readonly Func<Contest> _getCurrentContest;

        private readonly Func<long, string, Contest, bool> _registerParticipantForContest;
        private readonly Func<long, long, Contest, bool> _addReferral;
        private readonly Func<long, int, Participant> _getParticipant;
        private readonly Func<long, int, System.Collections.Generic.List<Participant>> _getReferrals;
        private readonly Action<long, long, Contest> _applyReferralEffects;

        public ReferralService(
            Func<string, Contest> getContestByCode,
            Func<Contest> getCurrentContest,
            Func<long, string, Contest, bool> registerParticipantForContest,
            Func<long, long, Contest, bool> addReferral,
            Func<long, int, Participant> getParticipant,
            Func<long, int, System.Collections.Generic.List<Participant>> getReferrals,
            Action<long, long, Contest> applyReferralEffects = null)
        {
            _getContestByCode = getContestByCode;
            _getCurrentContest = getCurrentContest;
            _registerParticipantForContest = registerParticipantForContest;
            _addReferral = addReferral;
            _getParticipant = getParticipant;
            _getReferrals = getReferrals;
            _applyReferralEffects = applyReferralEffects;
        }

        // Возвращает true если payload — это именно “реферальный вход” (ref_{contestCode}_{inviterId})
        public bool TryResolveInviter(string payload, long invitedUserId, out Contest contest, out long inviterId)
        {
            contest = null;
            inviterId = 0;

            if (string.IsNullOrWhiteSpace(payload))
                return false;

            payload = payload.Trim();

            // Формат: ref_{contestCode}_{inviterId}
            if (payload.StartsWith("ref_", StringComparison.OrdinalIgnoreCase))
            {
                var p = payload.Split('_');
                if (p.Length == 3 && !string.IsNullOrWhiteSpace(p[1]) && long.TryParse(p[2], out var inv))
                {
                    if (inv == invitedUserId) return false;

                    contest = _getContestByCode(p[1]);
                    if (contest == null) return false;

                    inviterId = inv;
                    return true;
                }

                return false;
            }

            return false;
        }

        // Засчитываем рефералку “как в твоём монолите”: регистрация обоих + AddReferral (без дублей)
        public bool TryCountReferral(long inviterId, long invitedUserId, string invitedUsername, Contest contest)
        {
            if (contest == null) return false;

            // Рефералка только для referral-конкурсов
            if (!string.Equals(contest.Type, "referral", StringComparison.OrdinalIgnoreCase))
                return false;

            // Только пока конкурс Running
            if (!string.Equals(contest.Status, "Running", StringComparison.OrdinalIgnoreCase))
                return false;

            // Без саморефа
            if (inviterId == invitedUserId)
                return false;

            // invited регистрируем всегда (если ещё не был)
            _registerParticipantForContest(invitedUserId, invitedUsername, contest);

            // inviter НЕ создаём автоматически.
            // Он должен уже быть участником (это твоя концепция из Unit9)
            var inviter = _getParticipant(inviterId, contest.Id);
            if (inviter == null)
                return false;

            // Быстрая защита от дублей по кэшу/списку
            var existing = _getReferrals(inviterId, contest.Id);
            if (existing != null && existing.Any(r => r.UserId == invitedUserId))
                return false;

            // Истина — БД. Если дубль или ошибка уникальности — вернёт false
            if (!_addReferral(inviterId, invitedUserId, contest))
                return false;

            // Если дошли сюда — реферал засчитан, применяем эффекты (счётчик/вес/кэш)
            _applyReferralEffects?.Invoke(inviterId, invitedUserId, contest);

            return true;
        }
    }
}

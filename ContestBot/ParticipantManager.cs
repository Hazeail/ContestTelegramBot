using System;
using System.Linq;
using System.Collections.Generic;

namespace ContestBot
{
    internal class ParticipantsManager
    {
        private readonly Dictionary<string, Participant> _participants = new Dictionary<string, Participant>();
        private readonly Dictionary<string, List<long>> _referralsByInviter = new Dictionary<string, List<long>>();
        private readonly ContestBot.Services.ReferralService _referralService;

        // Какие конкурсы уже загружены из БД в память (чтобы не грузить повторно)
        private readonly HashSet<int> _loadedContestIds = new HashSet<int>();


        private static string MakeKey(long userId, int contestId) => $"{userId}:{contestId}";

        private readonly ContestManager _contestManager;

        // Конструктор класса
        public ParticipantsManager(ContestManager contestManager)
        {
            _contestManager = contestManager;

            // 1. Пробуем получить текущий конкурс,
            //    который ContestManager уже загрузил из БД
            var currentContest = _contestManager.GetCurrentContest();
            if (currentContest != null)
            {
                // 1.1. Загружаем участников из БД
                try
                {
                    var loadedParticipants = Database.LoadParticipantsForContest(currentContest);
                    foreach (var participant in loadedParticipants)
                    {
                        participant.ContestId = currentContest.Id;
                        var key = MakeKey(participant.UserId, currentContest.Id);

                        if (!_participants.ContainsKey(key))
                            _participants[key] = participant;
                    }

                    Console.WriteLine(
                        $"[Participants] При старте загружено из БД {_participants.Count} участников для конкурса Id={currentContest.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Participants] Ошибка при загрузке участников из БД: " + ex.Message);
                }

                // 1.2. Загружаем реферальные связи из БД
                try
                {
                    var loadedReferrals = Database.LoadReferralsForContest(currentContest);
                    foreach (var kv in loadedReferrals)
                    {
                        long inviterId = kv.Key;
                        List<long> referredList = kv.Value;

                        string key = MakeKey(inviterId, currentContest.Id);

                        if (!_referralsByInviter.ContainsKey(key))
                            _referralsByInviter[key] = new List<long>();

                        foreach (var referredId in referredList)
                        {
                            if (!_referralsByInviter[key].Contains(referredId))
                                _referralsByInviter[key].Add(referredId);
                        }
                    }

                    Console.WriteLine(
                        $"[Participants] При старте загружено реферальных связей из БД: {_referralsByInviter.Count} пригласивших");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Participants] Ошибка при загрузке реферальных связей из БД: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("[Participants] При старте активного конкурса нет, участников/рефералов не загружаем.");
            }

            _referralService = new ContestBot.Services.ReferralService(
                getContestByCode: code => _contestManager.GetContestByCode(code),
                getCurrentContest: () => _contestManager.GetCurrentContest(),
                registerParticipantForContest: (userId, username, c) => RegisterParticipantForContest(userId, username, c),
                addReferral: (inv, invited, c) => Database.TryAddReferralRecord(inv, invited, c),
                getParticipant: (userId, contestId) => GetParticipant(userId, contestId),
                getReferrals: (inviterId, contestId) => GetReferrals(inviterId, contestId),
                applyReferralEffects: (inv, invited, c) => ApplyReferralEffects(inv, invited, c)
            );
        }
        private void EnsureContestLoaded(int contestId)
        {
            if (contestId <= 0)
                return;

            if (_loadedContestIds.Contains(contestId))
                return;

            var contest = _contestManager.GetContestById(contestId);
            if (contest == null)
            {
                Console.WriteLine($"[Participants] EnsureContestLoaded: contest #{contestId} not found in ContestManager.");
                return;
            }

            // 1) Участники
            try
            {
                var loadedParticipants = Database.LoadParticipantsForContest(contest);

                foreach (var participant in loadedParticipants)
                {
                    participant.ContestId = contest.Id;
                    var key = MakeKey(participant.UserId, contest.Id);

                    if (!_participants.ContainsKey(key))
                        _participants[key] = participant;
                }

                Console.WriteLine($"[Participants] Загружены участники для конкурса Id={contest.Id}: {loadedParticipants.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Participants] Ошибка при загрузке участников из БД: " + ex.Message);
            }

            // 2) Рефералы
            try
            {
                var loadedReferrals = Database.LoadReferralsForContest(contest);

                foreach (var kv in loadedReferrals)
                {
                    long inviterId = kv.Key;
                    var referredList = kv.Value;

                    string key = MakeKey(inviterId, contest.Id);

                    if (!_referralsByInviter.ContainsKey(key))
                        _referralsByInviter[key] = new List<long>();

                    foreach (var referredId in referredList)
                    {
                        if (!_referralsByInviter[key].Contains(referredId))
                            _referralsByInviter[key].Add(referredId);
                    }
                }

                Console.WriteLine($"[Participants] Загружены реф. связи для конкурса Id={contest.Id}: {loadedReferrals.Count} пригласивших");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Participants] Ошибка при загрузке реферальных связей из БД: " + ex.Message);
            }

            _loadedContestIds.Add(contestId);
        }


        // Логика выбора победителя с сохранением лога
        public Participant ChooseWinner(Random rng)
        {
            if (!_contestManager.HasActiveContest)
            {
                Console.WriteLine("[DRAW] Нет активного конкурса.");
                return null;
            }

            var contest = _contestManager.GetActiveContest();
            if (contest == null)
            {
                Console.WriteLine("[DRAW] Активный конкурс не найден.");
                return null;
            }

            // ✅ ВАЖНО: берём участников ТОЛЬКО этого конкурса
            var list = _participants.Values
                .Where(p => p.ContestId == contest.Id)
                .ToList();

            // ✅ ВОТ ЭТОГО У ТЕБЯ НЕ ХВАТАЛО:
            // если в конкурсе 0 участников — нельзя делать fallback по list[list.Count-1]
            if (list.Count == 0)
            {
                Console.WriteLine("[DRAW] Участников нет.");
                return null;
            }

            double totalWeight = 0;
            foreach (var p in list)
            {
                p.RecalculateWeight(contest);
                totalWeight += p.Weight;
            }

            if (totalWeight <= 0)
            {
                Console.WriteLine("[DRAW] Суммарный вес = 0.");
                return null;
            }

            Console.WriteLine($"[DRAW] Участников: {list.Count}, суммарный вес: {totalWeight:F2}");

            double r = rng.NextDouble() * totalWeight;
            Console.WriteLine($"[DRAW] r = {r:F4}");

            foreach (var p in list)
            {
                r -= p.Weight;
                if (r <= 0)
                {
                    Console.WriteLine($"[DRAW] Победитель: {p.UserId} (@{p.Username})");
                    return p;
                }
            }

            // fallback (на случай погрешности double)
            var fallback = list[list.Count - 1];
            Console.WriteLine($"[DRAW] Победитель (fallback): {fallback.UserId} (@{fallback.Username})");
            return fallback;
        }

        // Выбор нескольких победителей без повторов
        public List<Participant> ChooseWinners(Random rng, int winnersCount)
        {
            var result = new List<Participant>();

            var contest = _contestManager.GetCurrentContest();
            if (contest == null)
            {
                Console.WriteLine("[DRAW] Текущий конкурс не найден (множественный выбор).");
                return result;
            }

            EnsureContestLoaded(contest.Id);

            // ✅ ВАЖНО: пул ТОЛЬКО по этому конкурсу
            var pool = _participants.Values
                .Where(p => p.ContestId == contest.Id)
                .ToList();

            if (pool.Count == 0)
            {
                Console.WriteLine("[DRAW] Участников нет (множественный выбор).");
                return result;
            }

            if (winnersCount <= 0)
            {
                Console.WriteLine("[DRAW] winnersCount <= 0");
                return result;
            }

            // Выбор победителей вынесен в отдельную чистую логику (unit-тесты)
            return ContestBot.Services.Draw.WinnerSelector.SelectWinners(contest, pool, winnersCount, rng);
        }

        // Новый метод регистрации, Фиксим предыдущий метод...
        public bool RegisterParticipantForContest(long userId, string username, Contest contest, string firstName = null, string lastName = null)
        {
            if (contest == null)
                return false;

            EnsureContestLoaded(contest.Id);

            string key = MakeKey(userId, contest.Id);

            if (_participants.TryGetValue(key, out var existing))
            {
                if (!string.IsNullOrEmpty(username) && existing.Username != username)
                {
                    if (!string.IsNullOrWhiteSpace(firstName)) existing.FirstName = firstName;
                    if (!string.IsNullOrWhiteSpace(lastName)) existing.LastName = lastName;
                    try { Database.SaveOrUpdateParticipant(existing, contest); } catch { }

                    existing.Username = username;
                    try { Database.SaveOrUpdateParticipant(existing, contest); } catch { }
                }
                return false;
            }

            var p = new Participant
            {
                UserId = userId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                ContestId = contest.Id,
                ReferralCount = 0
            };

            p.RecalculateWeight(contest);
            _participants[key] = p;

            try
            {
                Database.SaveOrUpdateParticipant(p, contest);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] Error SaveOrUpdateParticipant(RegisterForContest): " + ex.Message);
            }

            return true;
        }

        public bool AddReferral(long inviterUserId, long referredUserId, Contest contest)
        {
            if (contest == null)
                return false;

            // Если invited уже зарегистрирован — берём username оттуда (иначе пусто)
            var invited = GetParticipant(referredUserId, contest.Id);
            var invitedUsername = invited != null ? invited.Username : null;

            return _referralService.TryCountReferral(inviterUserId, referredUserId, invitedUsername, contest);
        }

        // Отдаём всех участников (потом пригодится для выбора победителя)
        public IReadOnlyCollection<Participant> GetAllParticipants()
        {
            return _participants.Values;
        }
        // перегрузка
        public IReadOnlyCollection<Participant> GetAllParticipantsForContest(int contestId)
        {
            return _participants.Values.Where(p => p.ContestId == contestId).ToList();
        }

        // Получить участника по его userId (или null, если не найден)
        public Participant GetParticipant(long userId)
        {
            var contest = _contestManager.GetCurrentContest();
            if (contest == null)
                return null;

            string key = MakeKey(userId, contest.Id);
            return _participants.TryGetValue(key, out var p) ? p : null;
        }
        // перегрузка
        public Participant GetParticipant(long userId, int contestId)
        {
            EnsureContestLoaded(contestId);

            string key = MakeKey(userId, contestId);
            return _participants.TryGetValue(key, out var p) ? p : null;
        }

        // Получить список рефералов (как список Participant)
        public List<Participant> GetReferrals(long inviterUserId)
        {
            var contest = _contestManager.GetCurrentContest();
            if (contest == null)
                return new List<Participant>();

            string key = MakeKey(inviterUserId, contest.Id);

            if (!_referralsByInviter.TryGetValue(key, out var ids) || ids.Count == 0)
                return new List<Participant>();

            var result = new List<Participant>();
            foreach (var id in ids)
            {
                var p = GetParticipant(id, contest.Id);
                if (p != null)
                    result.Add(p);
            }

            return result;
        }
        //перегрузка
        public List<Participant> GetReferrals(long inviterUserId, int contestId)
        {
            EnsureContestLoaded(contestId);

            string key = MakeKey(inviterUserId, contestId);

            if (!_referralsByInviter.TryGetValue(key, out var ids) || ids.Count == 0)
                return new List<Participant>();

            var result = new List<Participant>();
            foreach (var id in ids)
            {
                var p = GetParticipant(id, contestId);
                if (p != null) result.Add(p);
            }
            return result;
        }

        private void ApplyReferralEffects(long inviterUserId, long referredUserId, Contest contest)
        {
            if (contest == null) return;

            EnsureContestLoaded(contest.Id);

            string inviterKey = MakeKey(inviterUserId, contest.Id);
            if (!_participants.TryGetValue(inviterKey, out var p))
                return; // inviter должен быть, но на всякий

            p.ReferralCount++;
            p.RecalculateWeight(contest);

            try
            {
                Database.SaveOrUpdateParticipant(p, contest);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] Error SaveOrUpdateParticipant(ApplyReferralEffects): " + ex.Message);
            }

            if (!_referralsByInviter.TryGetValue(inviterKey, out var list))
            {
                list = new List<long>();
                _referralsByInviter[inviterKey] = list;
            }

            if (!list.Contains(referredUserId))
                list.Add(referredUserId);
        }
    }
}
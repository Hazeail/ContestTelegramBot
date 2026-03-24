using System;
using System.Collections.Generic;
using System.Linq;

namespace ContestBot.Services.Draw
{
    internal static class WinnerSelector
    {
        // Выбор нескольких победителей по весам (без повторов).
        // Если суммарный вес получился 0 (например, не настроены веса) — используем равновероятный выбор.
        public static List<Participant> SelectWinners(Contest contest, IEnumerable<Participant> participants, int winnersCount, Random rng)
        {
            var result = new List<Participant>();

            if (contest == null) return result;
            if (participants == null) return result;
            if (winnersCount <= 0) return result;

            if (rng == null)
                rng = new Random();

            // Уникализируем по UserId и фиксируем порядок (чтобы поведение было стабильным)
            var pool = participants
                .Where(p => p != null)
                .GroupBy(p => p.UserId)
                .Select(g => g.First())
                .OrderBy(p => p.UserId)
                .ToList();

            if (pool.Count == 0) return result;

            if (winnersCount > pool.Count)
                winnersCount = pool.Count;

            for (int i = 0; i < winnersCount; i++)
            {
                double totalWeight = 0;

                for (int k = 0; k < pool.Count; k++)
                {
                    pool[k].RecalculateWeight(contest);
                    totalWeight += pool[k].Weight;
                }

                Participant winner;

                // Если веса не настроены или все получились 0 — выбираем равновероятно.
                if (totalWeight <= 0)
                {
                    winner = pool[rng.Next(pool.Count)];
                }
                else
                {
                    double r = rng.NextDouble() * totalWeight;
                    winner = null;

                    for (int k = 0; k < pool.Count; k++)
                    {
                        r -= pool[k].Weight;
                        if (r <= 0)
                        {
                            winner = pool[k];
                            break;
                        }
                    }

                    // Fallback на последний элемент на случай погрешности double
                    if (winner == null)
                        winner = pool[pool.Count - 1];
                }

                result.Add(winner);
                pool.Remove(winner);

                if (pool.Count == 0)
                    break;
            }

            return result;
        }
    }
}

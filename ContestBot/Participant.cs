using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContestBot
{
    internal class Participant
    {
        public long UserId { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public int ContestId { get; set; }           // в каком конкурсе участвует
        public int ReferralCount { get; set; }       // сколько он привёл людей
        public double Weight { get; set; }           // текущий вес в розыгрыше

        public void RecalculateWeight(Contest contest)
        {
            // Вес = базовый + рефералы * коэффициент
            double w = contest.BaseWeight + ReferralCount * contest.PerReferralWeight;

            // Ограничиваем максимум
            if (w > contest.MaxWeight)
                w = contest.MaxWeight;

            if (w < 0)
                w = 0; // подстраховка

            Weight = w;
        }

        public string GetDisplayNameWithoutId()
        {
            if (!string.IsNullOrWhiteSpace(Username))
                return "@" + Username.Trim().TrimStart('@');

            var full = ((FirstName ?? "").Trim() + " " + (LastName ?? "").Trim()).Trim();
            if (!string.IsNullOrWhiteSpace(full))
                return full;

            return null;
        }
    }
}


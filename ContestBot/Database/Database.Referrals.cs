using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot;

namespace ContestBot
{
    static partial class Database
    {
        // ---------- Добавление записи о реферале ----------
        public static void AddReferralRecord(long inviterUserId, long referredUserId, Contest contest)
        {
            if (contest == null)
                return;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    INSERT INTO Referrals (
                        InviterUserId,
                        ReferredUserId,
                        ContestId,
                        CreatedAt
                    ) VALUES (
                        @InviterUserId,
                        @ReferredUserId,
                        @ContestId,
                        @CreatedAt
                    );";
                    cmd.Parameters.AddWithValue("@InviterUserId", inviterUserId);
                    cmd.Parameters.AddWithValue("@ReferredUserId", referredUserId);
                    cmd.Parameters.AddWithValue("@ContestId", contest.Id);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));

                    cmd.ExecuteNonQuery();
                }
            }

            Console.WriteLine($"[DB] Referral saved: inviter={inviterUserId}, referred={referredUserId}, contestId={contest.Id}");
        }

        public static bool TryAddReferralRecord(long inviterId, long referredId, Contest contest)
        {
            if (contest == null)
                return false;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    // INSERT OR IGNORE + UNIQUE INDEX => дубль не вставится
                    cmd.CommandText = @"
                INSERT OR IGNORE INTO Referrals (
                    InviterUserId,
                    ReferredUserId,
                    ContestId,
                    CreatedAt
                ) VALUES (
                    @InviterId,
                    @ReferredId,
                    @ContestId,
                    @CreatedAt
                );";

                    cmd.Parameters.AddWithValue("@InviterId", inviterId);
                    cmd.Parameters.AddWithValue("@ReferredId", referredId);
                    cmd.Parameters.AddWithValue("@ContestId", contest.Id);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));

                    int affected = cmd.ExecuteNonQuery();

                    // affected == 1 => вставили (первый раз)
                    // affected == 0 => был дубль (проигнорировано)
                    return affected == 1;
                }
            }
        }

        /// <summary>
        /// Загружает все реферальные связи для конкурса:
        /// ключ — InviterUserId, значение — список ReferredUserId.
        /// </summary>
        public static Dictionary<long, List<long>> LoadReferralsForContest(Contest contest)
        {
            var result = new Dictionary<long, List<long>>();

            if (contest == null)
                return result;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    SELECT
                        InviterUserId,
                        ReferredUserId
                    FROM Referrals
                    WHERE ContestId = @ContestId;";

                    cmd.Parameters.AddWithValue("@ContestId", contest.Id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long inviterId = reader.GetInt64(0);
                            long referredId = reader.GetInt64(1);

                            if (!result.TryGetValue(inviterId, out var list))
                            {
                                list = new List<long>();
                                result[inviterId] = list;
                            }

                            if (!list.Contains(referredId))
                            {
                                list.Add(referredId);
                            }
                        }
                    }
                }
            }

            Console.WriteLine(
                "[DB] Загружены реферальные связи из БД для конкурса Id={0}: {1} пригласивших",
                contest.Id, result.Count);

            return result;
        }
    }
}

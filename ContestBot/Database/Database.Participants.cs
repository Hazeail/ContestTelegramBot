using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot;

namespace ContestBot
{
    static partial class Database
    {
        // ---------- Сохранение / обновление участника ----------
        public static void SaveOrUpdateParticipant(Participant participant, Contest contest)
        {
            if (participant == null || contest == null)
                return;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                // Проверяем, есть ли такой участник в этом конкурсе
                bool exists;
                using (var check = conn.CreateCommand())
                {
                    check.CommandText = @"
                    SELECT COUNT(*) 
                    FROM Participants 
                    WHERE UserId = @UserId AND ContestId = @ContestId;";
                    check.Parameters.AddWithValue("@UserId", participant.UserId);
                    check.Parameters.AddWithValue("@ContestId", contest.Id);

                    var count = Convert.ToInt32(check.ExecuteScalar());
                    exists = count > 0;
                }

                if (exists)
                {
                    using (var update = conn.CreateCommand())
                    {
                        update.CommandText = @"
                        UPDATE Participants
                        SET
                            Username      = @Username,
                            FirstName     = @FirstName,
                            LastName      = @LastName,
                            ReferralCount = @ReferralCount,
                            Weight        = @Weight
                        WHERE UserId = @UserId AND ContestId = @ContestId;";
                        FillParticipantParameters(update, participant, contest.Id);
                        update.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var insert = conn.CreateCommand())
                    {
                        insert.CommandText = @"
                        INSERT INTO Participants (
                            UserId,
                            Username,
                            FirstName,
                            LastName,
                            ReferralCount,
                            Weight,
                            ContestId
                        ) VALUES (
                            @UserId,
                            @Username,
                            @FirstName,
                            @LastName,
                            @ReferralCount,
                            @Weight,
                            @ContestId
                        );";
                        FillParticipantParameters(insert, participant, contest.Id);
                        insert.ExecuteNonQuery();
                    }
                }
            }

            Console.WriteLine($"[DB] Participant saved: UserId={participant.UserId}, ContestId={contest.Id}");
        }

        /// <summary>
        /// Загружает всех участников для указанного конкурса из таблицы Participants.
        /// Если конкурса нет или в таблице пусто – вернёт пустой список.
        /// </summary>
        public static List<Participant> LoadParticipantsForContest(Contest contest)
        {
            var result = new List<Participant>();

            if (contest == null)
                return result;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    SELECT
                        UserId,
                        Username,
                        FirstName,
                        LastName,
                        ReferralCount
                    FROM Participants
                    WHERE ContestId = @ContestId;";

                    cmd.Parameters.AddWithValue("@ContestId", contest.Id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long userId = reader.GetInt64(0);
                            string username = reader.IsDBNull(1) ? null : reader.GetString(1);
                            string firstName = reader.IsDBNull(2) ? null : reader.GetString(2);
                            string lastName = reader.IsDBNull(3) ? null : reader.GetString(3);
                            int referralCount = reader.GetInt32(4);

                            var p = new Participant
                            {
                                UserId = userId,
                                Username = username,
                                FirstName = firstName,
                                LastName = lastName,
                                ReferralCount = referralCount
                            };

                            // Пересчитаем вес по текущим настройкам конкурса
                            p.RecalculateWeight(contest);

                            result.Add(p);
                        }
                    }
                }
            }

            Console.WriteLine($"[DB] Загружено участников из БД для конкурса Id={contest.Id}: {result.Count}");

            return result;
        }

        // ---------- быстрый подсчёт участников (без загрузки списка) ----------
        public static int CountParticipantsForContestId(int contestId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Participants WHERE ContestId = @ContestId;";
                    cmd.Parameters.AddWithValue("@ContestId", contestId);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ---------- Вспомогательный метод для Participants ----------
        private static void FillParticipantParameters(SQLiteCommand cmd, Participant participant, int contestId)
        {
            cmd.Parameters.AddWithValue("@UserId", participant.UserId);
            cmd.Parameters.AddWithValue("@Username",
                (object)(participant.Username ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstName",
                (object)(participant.FirstName ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastName",
                (object)(participant.LastName ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReferralCount", participant.ReferralCount);
            cmd.Parameters.AddWithValue("@Weight", participant.Weight);
            cmd.Parameters.AddWithValue("@ContestId", contestId);
        }
    }
}

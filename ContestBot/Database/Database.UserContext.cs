using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot;

namespace ContestBot
{
    static partial class Database
    {
        // ---------- Контекст последнего конкурса для юзера ----------
        public static int? GetLastContestIdForUser(long telegramUserId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT ContestId
                        FROM UserContestContext
                        WHERE TelegramUserId = @UserId
                        ORDER BY UpdatedAt DESC
                        LIMIT 1;";

                    cmd.Parameters.AddWithValue("@UserId", telegramUserId);
                    var result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                        return null;

                    return Convert.ToInt32(result);
                }
            }
        }

        public static void SetLastContestIdForUser(long telegramUserId, int contestId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO UserContestContext (
                            TelegramUserId,
                            ContestId,
                            UpdatedAt
                        ) VALUES (
                            @UserId,
                            @ContestId,
                            @UpdatedAt
                        );";

                    cmd.Parameters.AddWithValue("@UserId", telegramUserId);
                    cmd.Parameters.AddWithValue("@ContestId", contestId);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static List<int> GetContestIdsForUser(long telegramUserId)
        {
            var result = new List<int>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT DISTINCT ContestId
                        FROM Participants
                        WHERE UserId = @UserId
                        ORDER BY ContestId DESC;
                    ";

                    cmd.Parameters.AddWithValue("@UserId", telegramUserId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // ContestId может быть NULL в старых данных — пропустим такие строки
                            if (!reader.IsDBNull(0))
                                result.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            return result;
        }
    }
}

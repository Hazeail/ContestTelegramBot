using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot;

namespace ContestBot
{
    static partial class Database
    {
        // ---------- Сохранение списка победителей для конкурса ----------
        public static void SaveWinners(Contest contest, List<Participant> winners)
        {
            if (contest == null || winners == null || winners.Count == 0)
                return;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                // Сначала очищаем старые записи победителей для этого конкурса,
                // чтобы при повторном розыгрыше не было дублей.
                using (var deleteCmd = conn.CreateCommand())
                {
                    deleteCmd.CommandText = @"
                        DELETE FROM ContestWinners
                        WHERE ContestId = @ContestId;";
                    deleteCmd.Parameters.AddWithValue("@ContestId", contest.Id);
                    deleteCmd.ExecuteNonQuery();
                }

                // Затем вставляем актуальный список победителей
                using (var insertCmd = conn.CreateCommand())
                {
                    insertCmd.CommandText = @"
                    INSERT INTO ContestWinners (
                        ContestId,
                        UserId,
                        Username,
                        FirstName,
                        LastName,
                        Position,
                        CreatedAt
                    ) VALUES (
                        @ContestId,
                        @UserId,
                        @Username,
                        @FirstName,
                        @LastName,
                        @Position,
                        @CreatedAt
                    );";

                    // одни параметры переиспользуем, значения будем менять
                    insertCmd.Parameters.Add("@ContestId", System.Data.DbType.Int32);
                    insertCmd.Parameters.Add("@UserId", System.Data.DbType.Int64);
                    insertCmd.Parameters.Add("@Username", System.Data.DbType.String);
                    insertCmd.Parameters.Add("@FirstName", System.Data.DbType.String);
                    insertCmd.Parameters.Add("@LastName", System.Data.DbType.String);
                    insertCmd.Parameters.Add("@Position", System.Data.DbType.Int32);
                    insertCmd.Parameters.Add("@CreatedAt", System.Data.DbType.String);

                    string now = DateTime.UtcNow.ToString("O");

                    for (int i = 0; i < winners.Count; i++)
                    {
                        var w = winners[i];

                        insertCmd.Parameters["@ContestId"].Value = contest.Id;
                        insertCmd.Parameters["@UserId"].Value = w.UserId;
                        insertCmd.Parameters["@Username"].Value =
                            (object)(w.Username ?? (string)null) ?? DBNull.Value;
                        insertCmd.Parameters["@FirstName"].Value = (object)(w.FirstName ?? (string)null) ?? DBNull.Value;
                        insertCmd.Parameters["@LastName"].Value = (object)(w.LastName ?? (string)null) ?? DBNull.Value;
                        insertCmd.Parameters["@Position"].Value = i + 1;
                        insertCmd.Parameters["@CreatedAt"].Value = now;

                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            Console.WriteLine(
                "[DB] Winners saved for ContestId={0}, Count={1}",
                contest.Id,
                winners.Count
            );
        }

        // ---------- Загрузка списка победителей для конкурса ----------
        // (нужно для unit-тестов и админской диагностики)
        public static List<Participant> LoadWinnersForContest(Contest contest)
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
                        SELECT UserId, Username, FirstName, LastName, Position
                        FROM ContestWinners
                        WHERE ContestId = @ContestId
                        ORDER BY Position;";
                    cmd.Parameters.AddWithValue("@ContestId", contest.Id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var p = new Participant();
                            p.UserId = reader.GetInt64(0);
                            p.Username = reader.IsDBNull(1) ? null : reader.GetString(1);
                            p.FirstName = reader.IsDBNull(2) ? null : reader.GetString(2);
                            p.LastName = reader.IsDBNull(3) ? null : reader.GetString(3);
                            p.ContestId = contest.Id;

                            result.Add(p);
                        }
                    }
                }
            }

            return result;
        }

        // ---------- быстрый подсчёт победителей (без загрузки списка) ----------
        public static int CountWinnersForContestId(int contestId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM ContestWinners WHERE ContestId = @ContestId;";
                    cmd.Parameters.AddWithValue("@ContestId", contestId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ---------- метод для очистки победителей (нужен для кейса “перевыбор → нет участников”) ----------
        public static void DeleteWinnersForContest(int contestId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM ContestWinners WHERE ContestId = @ContestId;";
                    cmd.Parameters.AddWithValue("@ContestId", contestId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

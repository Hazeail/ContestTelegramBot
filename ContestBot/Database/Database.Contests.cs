using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot;

namespace ContestBot
{
    static partial class Database
    {
        // ---------- сохранение конкурса ----------
        public static void SaveContest(Contest contest)
        {
            if (contest == null)
                return;

            // Если Id не задан — назначаем новый, иначе тесты/логика начинают жить на Id=0
            if (contest.Id <= 0)
                contest.Id = GetNextContestId();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                // Проверяем, есть ли уже запись с таким Id
                bool exists;
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT COUNT(*) FROM Contests WHERE Id = @Id;";
                    checkCmd.Parameters.AddWithValue("@Id", contest.Id);

                    var count = Convert.ToInt32(checkCmd.ExecuteScalar());
                    exists = count > 0;
                }

                if (exists)
                {
                    using (var updateCmd = conn.CreateCommand())
                    {
                        updateCmd.CommandText = @"
                        UPDATE Contests
                        SET
                            Name              = @Name,
                            Code              = @Code,
                            Type              = @Type,
                            Description       = @Description,
                            StartAt           = @StartAt,
                            EndAt             = @EndAt,
                            Status            = @Status,
                            BaseWeight        = @BaseWeight,
                            PerReferralWeight = @PerReferralWeight,
                            MaxWeight         = @MaxWeight,
                            WinnersCount      = @WinnersCount,
                            ImageFileId       = @ImageFileId,
                            MediaType          = @MediaType,
                            MediaFileId        = @MediaFileId,
                            ChannelId = @ChannelId,
                            ChannelUsername = @ChannelUsername,
                            ChannelPostMessageId = @ChannelPostMessageId

                        WHERE Id = @Id;";

                        FillContestParameters(updateCmd, contest);
                        updateCmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var insertCmd = conn.CreateCommand())
                    {
                        insertCmd.CommandText = @"
                        INSERT INTO Contests (
                            Id,
                            Code,
                            Name,
                            Type,
                            Description,
                            StartAt,
                            EndAt,
                            Status,
                            BaseWeight,
                            PerReferralWeight,
                            MaxWeight,
                            WinnersCount,
                            ImageFileId,
                            MediaType,
                            MediaFileId,
                            ChannelId,
                            ChannelUsername,
                            ChannelPostMessageId,
                            CreatedByAdminUserId
                        ) VALUES (
                            @Id,
                            @Code,
                            @Name,
                            @Type,
                            @Description,
                            @StartAt,
                            @EndAt,
                            @Status,
                            @BaseWeight,
                            @PerReferralWeight,
                            @MaxWeight,
                            @WinnersCount,
                            @ImageFileId,
                            @MediaType,
                            @MediaFileId,
                            @ChannelId,
                            @ChannelUsername,
                            @ChannelPostMessageId,
                            @CreatedByAdminUserId
                        );";

                        FillContestParameters(insertCmd, contest);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            Console.WriteLine("[DB] Конкурс сохранён в SQLite (Id={0}, Status={1})",
                contest.Id, contest.Status);
        }

        // ---------- загрузка последнего конкурса ----------
        public static Contest LoadLastContest()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    // Берём последний по StartAt (они в формате 'O' — ISO 8601, сортируются нормально)
                    cmd.CommandText = @"
                    SELECT
                        Id,
                        Code,
                        Name,
                        Type,
                        Description,
                        StartAt,
                        EndAt,
                        Status,
                        BaseWeight,
                        PerReferralWeight,
                        MaxWeight,
                        WinnersCount,
                        ImageFileId,
                        MediaType,
                        MediaFileId,
                        ChannelId,
                        ChannelUsername,
                        ChannelPostMessageId,
                        CreatedByAdminUserId
                    FROM Contests
                    ORDER BY StartAt DESC
                    LIMIT 1;";

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        var contest = new Contest();
                        contest.Id = reader.GetInt32(0);
                        contest.Code = reader.IsDBNull(1) ? null : reader.GetString(1);
                        contest.Name = reader.GetString(2);
                        contest.Type = reader.GetString(3);
                        contest.Description = reader.IsDBNull(4) ? null : reader.GetString(4);

                        var startStr = reader.GetString(5);
                        var endStr = reader.GetString(6);
                        contest.StartAt = DateTime.Parse(startStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        contest.EndAt = DateTime.Parse(endStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

                        contest.Status = reader.GetString(7);
                        contest.BaseWeight = reader.GetDouble(8);
                        contest.PerReferralWeight = reader.GetDouble(9);
                        contest.MaxWeight = reader.GetDouble(10);
                        contest.WinnersCount = reader.GetInt32(11);
                        contest.ImageFileId = reader.IsDBNull(12) ? null : reader.GetString(12);
                        contest.MediaType = reader.IsDBNull(13) ? null : reader.GetString(13);
                        contest.MediaFileId = reader.IsDBNull(14) ? null : reader.GetString(14);
                        contest.ChannelId = reader.IsDBNull(15) ? (long?)null : reader.GetInt64(15);
                        contest.ChannelUsername = reader.IsDBNull(16) ? null : reader.GetString(16);
                        contest.ChannelPostMessageId = reader.IsDBNull(17) ? (int?)null : reader.GetInt32(17);
                        contest.CreatedByAdminUserId = reader.IsDBNull(18) ? (long?)null : reader.GetInt64(18);

                        Console.WriteLine("[DB] Загружен конкурс из SQLite (Id={0}, Status={1})",
                            contest.Id, contest.Status);

                        return contest;
                    }
                }
            }
        }

        // ---------- загрузка конкурса по Id ----------
        public static Contest LoadContestById(int contestId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    SELECT
                        Id,
                        Code,
                        Name,
                        Type,
                        Description,
                        StartAt,
                        EndAt,
                        Status,
                        BaseWeight,
                        PerReferralWeight,
                        MaxWeight,
                        WinnersCount,
                        ImageFileId,
                        MediaType,
                        MediaFileId,
                        ChannelId,
                        ChannelUsername,
                        ChannelPostMessageId,
                        CreatedByAdminUserId
                    FROM Contests
                    WHERE Id = @Id
                    LIMIT 1;";

                    cmd.Parameters.AddWithValue("@Id", contestId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        var contest = new Contest();
                        contest.Id = reader.GetInt32(0);
                        contest.Code = reader.IsDBNull(1) ? null : reader.GetString(1);
                        contest.Name = reader.GetString(2);
                        contest.Type = reader.GetString(3);
                        contest.Description = reader.IsDBNull(4) ? null : reader.GetString(4);

                        var startStr = reader.GetString(5);
                        var endStr = reader.GetString(6);
                        contest.StartAt = DateTime.Parse(startStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        contest.EndAt = DateTime.Parse(endStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

                        contest.Status = reader.GetString(7);
                        contest.BaseWeight = reader.GetDouble(8);
                        contest.PerReferralWeight = reader.GetDouble(9);
                        contest.MaxWeight = reader.GetDouble(10);
                        contest.WinnersCount = reader.GetInt32(11);
                        contest.ImageFileId = reader.IsDBNull(12) ? null : reader.GetString(12);
                        contest.MediaType = reader.IsDBNull(13) ? null : reader.GetString(13);
                        contest.MediaFileId = reader.IsDBNull(14) ? null : reader.GetString(14);
                        contest.ChannelId = reader.IsDBNull(15) ? (long?)null : reader.GetInt64(15);
                        contest.ChannelUsername = reader.IsDBNull(16) ? null : reader.GetString(16);
                        contest.ChannelPostMessageId = reader.IsDBNull(17) ? (int?)null : reader.GetInt32(17);
                        contest.CreatedByAdminUserId = reader.IsDBNull(18) ? (long?)null : reader.GetInt64(18);

                        return contest;
                    }
                }
            }
        }

        // ---------- упрощённая загрузка списка конкурсов для админ-меню ----------
        // Берём только то, что нужно для списка: Id/Name/Status.
        public static List<Contest> LoadContestsForAdminList()
        {
            var list = new List<Contest>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT Id, Name, Status
                        FROM Contests
                        WHERE Status = 'Running' OR Status = 'Finished'
                        ORDER BY Id DESC;";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var c = new Contest();
                            c.Id = reader.GetInt32(0);
                            c.Name = reader.IsDBNull(1) ? null : reader.GetString(1);
                            c.Status = reader.IsDBNull(2) ? null : reader.GetString(2);
                            list.Add(c);
                        }
                    }
                }
            }

            return list;
        }

        // поддержка многокункурсности
        public static int GetNextContestId()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT IFNULL(MAX(Id), 0) FROM Contests;";
                    int max = Convert.ToInt32(cmd.ExecuteScalar());
                    return max + 1;
                }
            }
        }

        // метод загрузки ВСЕХ конкурсов
        public static List<Contest> LoadAllContests()
        {
            var list = new List<Contest>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            Id, Code, Name, Type, Description,
                            StartAt, EndAt, Status,
                            BaseWeight, PerReferralWeight, MaxWeight,
                            WinnersCount, ImageFileId,
                            MediaType, MediaFileId, ChannelId,
                            ChannelUsername, ChannelPostMessageId, CreatedByAdminUserId
                        FROM Contests
                        ORDER BY Id DESC;
                    ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var c = new Contest();
                            c.Id = reader.GetInt32(0);
                            c.Code = reader.IsDBNull(1) ? null : reader.GetString(1);
                            c.Name = reader.GetString(2);
                            c.Type = reader.GetString(3);
                            c.Description = reader.IsDBNull(4) ? null : reader.GetString(4);

                            c.StartAt = DateTime.Parse(reader.GetString(5),
                                null,
                                System.Globalization.DateTimeStyles.RoundtripKind);

                            c.EndAt = DateTime.Parse(reader.GetString(6),
                                null,
                                System.Globalization.DateTimeStyles.RoundtripKind);

                            c.Status = reader.GetString(7);
                            c.BaseWeight = reader.GetDouble(8);
                            c.PerReferralWeight = reader.GetDouble(9);
                            c.MaxWeight = reader.GetDouble(10);
                            c.WinnersCount = reader.GetInt32(11);
                            c.ImageFileId = reader.IsDBNull(12) ? null : reader.GetString(12);
                            c.MediaType = reader.IsDBNull(13) ? null : reader.GetString(13);
                            c.MediaFileId = reader.IsDBNull(14) ? null : reader.GetString(14);
                            c.ChannelId = reader.IsDBNull(15) ? (long?)null : reader.GetInt64(15);
                            c.ChannelUsername = reader.IsDBNull(16) ? null : reader.GetString(16);
                            c.ChannelPostMessageId = reader.IsDBNull(17) ? (int?)null : reader.GetInt32(17);
                            c.CreatedByAdminUserId = reader.IsDBNull(18) ? (long?)null : reader.GetInt64(18);


                            list.Add(c);
                        }
                    }
                }
            }

            return list;
        }

        // ---------- Вспомогательный метод ----------
        private static void FillContestParameters(SQLiteCommand cmd, Contest contest)
        {
            cmd.Parameters.AddWithValue("@Id", contest.Id);
            cmd.Parameters.AddWithValue("@Code", contest.Code ?? $"c{contest.Id}");
            cmd.Parameters.AddWithValue("@Name", contest.Name ?? "");
            cmd.Parameters.AddWithValue("@Type", contest.Type ?? "");
            cmd.Parameters.AddWithValue("@Description",
                (object)(contest.Description ?? (string)null) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@StartAt", contest.StartAt.ToString("O"));
            cmd.Parameters.AddWithValue("@EndAt", contest.EndAt.ToString("O"));

            cmd.Parameters.AddWithValue("@Status", contest.Status ?? "Draft");
            cmd.Parameters.AddWithValue("@BaseWeight", contest.BaseWeight);
            cmd.Parameters.AddWithValue("@PerReferralWeight", contest.PerReferralWeight);
            cmd.Parameters.AddWithValue("@MaxWeight", contest.MaxWeight);
            cmd.Parameters.AddWithValue("@WinnersCount", contest.WinnersCount);

            cmd.Parameters.AddWithValue("@MediaType", (object)(contest.MediaType ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MediaFileId", (object)(contest.MediaFileId ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ChannelId", contest.ChannelId.HasValue ? (object)contest.ChannelId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ChannelUsername", (object)(contest.ChannelUsername ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ChannelPostMessageId", contest.ChannelPostMessageId.HasValue ? (object)contest.ChannelPostMessageId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue(
                "@CreatedByAdminUserId",
                contest.CreatedByAdminUserId.HasValue ? (object)contest.CreatedByAdminUserId.Value : DBNull.Value
            );

            if (string.IsNullOrEmpty(contest.ImageFileId))
                cmd.Parameters.AddWithValue("@ImageFileId", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("@ImageFileId", contest.ImageFileId);
        }

        // ---------- метод загрузки черновика ----------
        public static Contest LoadDraftContestByAdminUserId(long adminUserId)
        {
            var all = LoadAllContests();
            Contest best = null;

            foreach (var c in all)
            {
                if (c == null) continue;
                if (!string.Equals(c.Status, "Draft", StringComparison.OrdinalIgnoreCase)) continue;
                if (c.CreatedByAdminUserId != adminUserId) continue;

                if (best == null || c.Id > best.Id)
                    best = c;
            }

            return best;
        }

        // ---------- метод удаления конкурса по Id ----------
        public static void DeleteContestById(int contestId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Contests WHERE Id = @Id;";
                    cmd.Parameters.AddWithValue("@Id", contestId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

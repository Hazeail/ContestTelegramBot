using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot.Admin.Creation;

namespace ContestBot
{
    static partial class Database
    {
        internal sealed class ContestDraftRow
        {
            public long DraftId { get; set; }
            public long AdminUserId { get; set; }
            public ContestCreationState State { get; set; }
            public Contest Draft { get; set; }

            public bool IsActive { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
        }

        internal sealed class ContestDraftListItem
        {
            public long DraftId { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public ContestCreationState State { get; set; }
            public bool IsActive { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
        }

        // Создать новый черновик (возвращает DraftId)
        public static long CreateContestDraft(long adminUserId, ContestCreationState state, Contest draft)
        {
            if (draft == null) draft = new Contest();

            var nowUtc = DateTime.UtcNow;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO ContestDrafts
                        (
                            AdminUserId, State,
                            Name, Type, Description,
                            StartAt, EndAt, Status,
                            BaseWeight, PerReferralWeight, MaxWeight,
                            WinnersCount,
                            ImageFileId, MediaType, MediaFileId,
                            ChannelId, ChannelUsername,
                            IsActive, CreatedAtUtc, UpdatedAtUtc
                        )
                        VALUES
                        (
                            @AdminUserId, @State,
                            @Name, @Type, @Description,
                            @StartAt, @EndAt, @Status,
                            @BaseWeight, @PerReferralWeight, @MaxWeight,
                            @WinnersCount,
                            @ImageFileId, @MediaType, @MediaFileId,
                            @ChannelId, @ChannelUsername,
                            1, @CreatedAtUtc, @UpdatedAtUtc
                        );
                        SELECT last_insert_rowid();
                    ";

                    FillDraftParameters(cmd, adminUserId, state, draft, nowUtc, nowUtc);

                    var idObj = cmd.ExecuteScalar();
                    return Convert.ToInt64(idObj);
                }
            }
        }

        // Обновить существующий черновик
        public static void UpdateContestDraft(long draftId, long adminUserId, ContestCreationState state, Contest draft)
        {
            if (draft == null) return;

            var nowUtc = DateTime.UtcNow;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE ContestDrafts
                        SET
                            State = @State,
                            Name = @Name,
                            Type = @Type,
                            Description = @Description,
                            StartAt = @StartAt,
                            EndAt = @EndAt,
                            Status = @Status,
                            BaseWeight = @BaseWeight,
                            PerReferralWeight = @PerReferralWeight,
                            MaxWeight = @MaxWeight,
                            WinnersCount = @WinnersCount,
                            ImageFileId = @ImageFileId,
                            MediaType = @MediaType,
                            MediaFileId = @MediaFileId,
                            ChannelId = @ChannelId,
                            ChannelUsername = @ChannelUsername,
                            UpdatedAtUtc = @UpdatedAtUtc
                        WHERE DraftId = @DraftId AND AdminUserId = @AdminUserId AND IsActive = 1;
                    ";

                    cmd.Parameters.AddWithValue("@DraftId", draftId);
                    FillDraftParameters(cmd, adminUserId, state, draft, null, nowUtc);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Загрузить черновик по DraftId (только свой админ)
        public static ContestDraftRow LoadContestDraft(long adminUserId, long draftId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            DraftId, AdminUserId, State,
                            Name, Type, Description,
                            StartAt, EndAt, Status,
                            BaseWeight, PerReferralWeight, MaxWeight,
                            WinnersCount,
                            ImageFileId, MediaType, MediaFileId,
                            ChannelId, ChannelUsername,
                            IsActive, CreatedAtUtc, UpdatedAtUtc
                        FROM ContestDrafts
                        WHERE DraftId = @DraftId AND AdminUserId = @AdminUserId;
                    ";
                    cmd.Parameters.AddWithValue("@DraftId", draftId);
                    cmd.Parameters.AddWithValue("@AdminUserId", adminUserId);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return null;

                        var row = new ContestDraftRow();
                        row.DraftId = Convert.ToInt64(r["DraftId"]);
                        row.AdminUserId = Convert.ToInt64(r["AdminUserId"]);
                        row.State = (ContestCreationState)Convert.ToInt32(r["State"]);
                        row.IsActive = Convert.ToInt32(r["IsActive"]) == 1;

                        row.CreatedAtUtc = ParseUtc(r["CreatedAtUtc"]);
                        row.UpdatedAtUtc = ParseUtc(r["UpdatedAtUtc"]);

                        row.Draft = new Contest
                        {
                            Name = Convert.ToString(r["Name"]),
                            Type = Convert.ToString(r["Type"]),
                            Description = Convert.ToString(r["Description"]),
                            Status = Convert.ToString(r["Status"]),
                            BaseWeight = Convert.ToDouble(r["BaseWeight"]),
                            PerReferralWeight = Convert.ToDouble(r["PerReferralWeight"]),
                            MaxWeight = Convert.ToDouble(r["MaxWeight"]),
                            WinnersCount = Convert.ToInt32(r["WinnersCount"]),
                            ImageFileId = DbNullToNullString(r["ImageFileId"]),
                            MediaType = DbNullToNullString(r["MediaType"]),
                            MediaFileId = DbNullToNullString(r["MediaFileId"]),
                            ChannelId = DbNullToNullableLong(r["ChannelId"]),
                            ChannelUsername = DbNullToNullString(r["ChannelUsername"])
                        };

                        row.Draft.StartAt = ParseOrMin(r["StartAt"]);
                        row.Draft.EndAt = ParseOrMin(r["EndAt"]);
                        row.Draft.CreatedByAdminUserId = adminUserId;

                        return row;
                    }
                }
            }
        }

        // Список черновиков админа
        public static List<ContestDraftListItem> ListContestDrafts(long adminUserId, bool onlyActive)
        {
            var list = new List<ContestDraftListItem>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT DraftId, Name, Type, State, IsActive, UpdatedAtUtc
                        FROM ContestDrafts
                        WHERE AdminUserId = @AdminUserId
                          AND (@OnlyActive = 0 OR IsActive = 1)
                        ORDER BY UpdatedAtUtc DESC;
                    ";
                    cmd.Parameters.AddWithValue("@AdminUserId", adminUserId);
                    cmd.Parameters.AddWithValue("@OnlyActive", onlyActive ? 1 : 0);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new ContestDraftListItem
                            {
                                DraftId = Convert.ToInt64(r["DraftId"]),
                                Name = Convert.ToString(r["Name"]),
                                Type = Convert.ToString(r["Type"]),
                                State = (ContestCreationState)Convert.ToInt32(r["State"]),
                                IsActive = Convert.ToInt32(r["IsActive"]) == 1,
                                UpdatedAtUtc = ParseUtc(r["UpdatedAtUtc"])
                            });
                        }
                    }
                }
            }

            return list;
        }

        // Пометить черновик неактивным (например: опубликован/отменён)
        public static void DeactivateContestDraft(long adminUserId, long draftId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE ContestDrafts
                        SET IsActive = 0, UpdatedAtUtc = @UpdatedAtUtc
                        WHERE DraftId = @DraftId AND AdminUserId = @AdminUserId;
                    ";
                    cmd.Parameters.AddWithValue("@DraftId", draftId);
                    cmd.Parameters.AddWithValue("@AdminUserId", adminUserId);
                    cmd.Parameters.AddWithValue("@UpdatedAtUtc", DateTime.UtcNow.ToString("O"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void FillDraftParameters(SQLiteCommand cmd, long adminUserId, ContestCreationState state, Contest draft, DateTime? createdUtc, DateTime updatedUtc)
        {
            cmd.Parameters.AddWithValue("@AdminUserId", adminUserId);
            cmd.Parameters.AddWithValue("@State", (int)state);

            cmd.Parameters.AddWithValue("@Name", (object)(draft.Name ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Type", (object)(draft.Type ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", (object)(draft.Description ?? (string)null) ?? DBNull.Value);

            // Start/End могут быть не заданы в черновике
            cmd.Parameters.AddWithValue("@StartAt", draft.StartAt == DateTime.MinValue ? (object)DBNull.Value : draft.StartAt.ToString("O"));
            cmd.Parameters.AddWithValue("@EndAt", draft.EndAt == DateTime.MinValue ? (object)DBNull.Value : draft.EndAt.ToString("O"));

            cmd.Parameters.AddWithValue("@Status", (object)(draft.Status ?? (string)null) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@BaseWeight", draft.BaseWeight);
            cmd.Parameters.AddWithValue("@PerReferralWeight", draft.PerReferralWeight);
            cmd.Parameters.AddWithValue("@MaxWeight", draft.MaxWeight);

            cmd.Parameters.AddWithValue("@WinnersCount", draft.WinnersCount);

            cmd.Parameters.AddWithValue("@ImageFileId", (object)(draft.ImageFileId ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MediaType", (object)(draft.MediaType ?? (string)null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MediaFileId", (object)(draft.MediaFileId ?? (string)null) ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@ChannelId", draft.ChannelId.HasValue ? (object)draft.ChannelId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ChannelUsername", (object)(draft.ChannelUsername ?? (string)null) ?? DBNull.Value);

            if (createdUtc.HasValue)
                cmd.Parameters.AddWithValue("@CreatedAtUtc", createdUtc.Value.ToString("O"));
            if (cmd.CommandText.Contains("@UpdatedAtUtc"))
                cmd.Parameters.AddWithValue("@UpdatedAtUtc", updatedUtc.ToString("O"));
        }

        private static DateTime ParseOrMin(object value)
        {
            var s = DbNullToNullString(value);
            if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;
            if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return DateTime.MinValue;
        }

        private static DateTime ParseUtc(object value)
        {
            var s = DbNullToNullString(value);
            if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;
            if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return DateTime.MinValue;
        }

        private static string DbNullToNullString(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToString(v);
        }

        private static long? DbNullToNullableLong(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt64(v);
        }
    }
}

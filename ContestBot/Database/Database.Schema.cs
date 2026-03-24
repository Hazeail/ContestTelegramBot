using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot;

namespace ContestBot
{
    static partial class Database
    {
        public static void EnsureCreated()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                                           CREATE TABLE IF NOT EXISTS Contests (
                        Id                INTEGER PRIMARY KEY,
                        Code              TEXT,               -- новый: c1, c2, ...
                        Name              TEXT NOT NULL,
                        Type              TEXT NOT NULL,
                        Description       TEXT,
                        StartAt           TEXT NOT NULL,
                        EndAt             TEXT NOT NULL,
                        Status            TEXT NOT NULL,
                        BaseWeight        REAL NOT NULL,
                        PerReferralWeight REAL NOT NULL,
                        MaxWeight         REAL NOT NULL,
                        WinnersCount      INTEGER NOT NULL,
                        ImageFileId       TEXT
                    );

                    CREATE TABLE IF NOT EXISTS Participants (
                        Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId        INTEGER NOT NULL,
                        Username      TEXT,
                        FirstName     TEXT,
                        LastName      TEXT,
                        ReferralCount INTEGER NOT NULL,
                        Weight        REAL NOT NULL,
                        ContestId     INTEGER,
                        FOREIGN KEY (ContestId) REFERENCES Contests(Id)
                    );

                        CREATE TABLE IF NOT EXISTS Referrals (
                            Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                            InviterUserId  INTEGER NOT NULL,
                            ReferredUserId INTEGER NOT NULL,
                            ContestId      INTEGER,
                            CreatedAt      TEXT NOT NULL,
                            FOREIGN KEY (ContestId) REFERENCES Contests(Id)
                        );

                        CREATE TABLE IF NOT EXISTS Winners (
                            Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                            ContestId INTEGER NOT NULL,
                            UserId    INTEGER NOT NULL,
                            Username  TEXT,
                            Position  INTEGER NOT NULL,
                            CreatedAt TEXT NOT NULL,
                            FOREIGN KEY (ContestId) REFERENCES Contests(Id)
                        );

                        CREATE TABLE IF NOT EXISTS ContestWinners (
                            Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                            ContestId INTEGER NOT NULL,
                            UserId    INTEGER NOT NULL,
                            Username  TEXT,
                            FirstName TEXT,
                            LastName  TEXT,
                            Position  INTEGER NOT NULL,
                            CreatedAt TEXT NOT NULL,
                            FOREIGN KEY (ContestId) REFERENCES Contests(Id)
                        );

                        CREATE INDEX IF NOT EXISTS IX_ContestWinners_ContestId ON ContestWinners(ContestId);

                        -- контекст последнего конкурса для каждого юзера
                        CREATE TABLE IF NOT EXISTS UserContestContext (
                            Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                            TelegramUserId INTEGER NOT NULL,
                            ContestId      INTEGER NOT NULL,
                            UpdatedAt      TEXT NOT NULL,
                            FOREIGN KEY (ContestId) REFERENCES Contests(Id)
                        );
                        
                        CREATE UNIQUE INDEX IF NOT EXISTS idx_referrals_unique
                            ON Referrals (InviterUserId, ReferredUserId, ContestId);

                                                -- черновики конструктора конкурсов (могут быть несколько на одного админа)
                        CREATE TABLE IF NOT EXISTS ContestDrafts (
                            DraftId            INTEGER PRIMARY KEY AUTOINCREMENT,
                            AdminUserId         INTEGER NOT NULL,
                            State               INTEGER NOT NULL, -- ContestCreationState (int)

                            Name                TEXT,
                            Type                TEXT,
                            Description         TEXT,

                            StartAt             TEXT,
                            EndAt               TEXT,
                            Status              TEXT,

                            BaseWeight           REAL NOT NULL,
                            PerReferralWeight    REAL NOT NULL,
                            MaxWeight            REAL NOT NULL,

                            WinnersCount         INTEGER NOT NULL,

                            ImageFileId          TEXT,
                            MediaType            TEXT,
                            MediaFileId          TEXT,

                            ChannelId            INTEGER,
                            ChannelUsername      TEXT,

                            IsActive             INTEGER NOT NULL,
                            CreatedAtUtc         TEXT NOT NULL,
                            UpdatedAtUtc         TEXT NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS IX_ContestDrafts_AdminUserId ON ContestDrafts(AdminUserId);
                        CREATE INDEX IF NOT EXISTS IX_ContestDrafts_IsActive ON ContestDrafts(IsActive);

                    ";
                    cmd.ExecuteNonQuery();

                    try
                    {
                        using (var migrate = conn.CreateCommand())
                        {
                            migrate.CommandText = @"
                                INSERT INTO ContestWinners (ContestId, UserId, Username, Position, CreatedAt)
                                SELECT w.ContestId, w.UserId, w.Username, w.Position, w.CreatedAt
                                FROM Winners w
                                WHERE NOT EXISTS (
                                    SELECT 1 FROM ContestWinners cw WHERE cw.ContestId = w.ContestId
                                );
                            ";
                            migrate.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DB MIGRATION] Winners -> ContestWinners failed: " + ex.Message);
                    }

                    // Admins schema (kept as a separate command to avoid accidental overwrite of the main schema).
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Admins (
                            UserId   INTEGER PRIMARY KEY,
                            AddedAt  TEXT NOT NULL,
                            AddedBy  INTEGER NOT NULL,
                            IsActive INTEGER NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS IX_Admins_IsActive ON Admins(IsActive);
                    ";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Channels (
                            ChannelId          INTEGER PRIMARY KEY,
                            Username           TEXT,
                            Title              TEXT,
                            IsActive           INTEGER NOT NULL,
                            AddedByAdminUserId INTEGER NOT NULL,
                            AddedAtUtc         TEXT NOT NULL
                        );

                        CREATE INDEX IF NOT EXISTS IX_Channels_IsActive ON Channels(IsActive);
                    ";
                    cmd.ExecuteNonQuery();

                    try
                    {
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText = "ALTER TABLE Participants ADD COLUMN FirstName TEXT;";
                            alter.ExecuteNonQuery();
                        }
                    }
                    catch { }

                    try
                    {
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText = "ALTER TABLE Participants ADD COLUMN LastName TEXT;";
                            alter.ExecuteNonQuery();
                        }
                    }
                    catch { }

                    try
                    {
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText = "ALTER TABLE ContestWinners ADD COLUMN FirstName TEXT;";
                            alter.ExecuteNonQuery();
                        }
                    }
                    catch { }

                    try
                    {
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText = "ALTER TABLE ContestWinners ADD COLUMN LastName TEXT;";
                            alter.ExecuteNonQuery();
                        }
                    }
                    catch { }

                    // --- simple migrations (SQLite) ---
                    TryAddColumn(conn, "Contests", "MediaType", "TEXT");
                    TryAddColumn(conn, "Contests", "MediaFileId", "TEXT");
                    TryAddColumn(conn, "Contests", "ChannelId", "INTEGER");
                    TryAddColumn(conn, "Contests", "ChannelUsername", "TEXT");
                    TryAddColumn(conn, "Contests", "ChannelPostMessageId", "INTEGER");

                    TryAddColumn(conn, "Contests", "CreatedByAdminUserId", "INTEGER");

                    // Admins profile fields (for nicer UI; safe to call repeatedly)
                    TryAddColumn(conn, "Admins", "Username", "TEXT");
                    TryAddColumn(conn, "Admins", "FirstName", "TEXT");
                    TryAddColumn(conn, "Admins", "LastName", "TEXT");


                }
            }
        }

        /// <summary>
        /// Adds a column to an existing table if it doesn't exist.
        /// Safe to call repeatedly.
        /// </summary>
        private static void TryAddColumn(SQLiteConnection conn, string tableName, string columnName, string sqliteType)
        {
            if (conn == null) return;
            if (string.IsNullOrWhiteSpace(tableName)) return;
            if (string.IsNullOrWhiteSpace(columnName)) return;
            if (string.IsNullOrWhiteSpace(sqliteType)) return;

            try
            {
                // Check existing columns
                bool exists = false;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(" + tableName + ");";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var name = Convert.ToString(reader["name"]);
                            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                }

                if (exists) return;

                using (var alter = conn.CreateCommand())
                {
                    alter.CommandText = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + sqliteType + ";";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Don't crash bot startup because of migration.
                Console.WriteLine("[DB MIGRATION] TryAddColumn failed: " + ex.Message);
            }
        }
    }
}

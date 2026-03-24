using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot.Admins;

namespace ContestBot
{
    static partial class Database
    {
        public static bool IsActiveAdmin(long userId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "SELECT 1 FROM Admins WHERE UserId=@uid AND IsActive=1 LIMIT 1;";
                    cmd.Parameters.AddWithValue("@uid", userId);

                    var r = cmd.ExecuteScalar();
                    return r != null;
                }
            }
        }

        public static List<long> LoadActiveAdmins()
        {
            var result = new List<long>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "SELECT UserId FROM Admins WHERE IsActive=1;";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            result.Add(reader.GetInt64(0));
                    }
                }
            }

            return result;
        }

        public static List<AdminProfile> LoadActiveAdminProfiles()
        {
            var result = new List<AdminProfile>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "SELECT UserId, Username, FirstName, LastName FROM Admins WHERE IsActive=1;";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var p = new AdminProfile
                            {
                                UserId = reader.GetInt64(0),
                                Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                                FirstName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                LastName = reader.IsDBNull(3) ? null : reader.GetString(3)
                            };
                            result.Add(p);
                        }
                    }
                }
            }

            return result;
        }

        public static void UpsertAdminProfile(long userId, long addedBy, string username, string firstName, string lastName)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = @"
                    INSERT INTO Admins(UserId, AddedAt, AddedBy, IsActive, Username, FirstName, LastName)
                    VALUES(@uid, @at, @by, 1, @un, @fn, @ln)
                    ON CONFLICT(UserId) DO UPDATE SET
                        AddedAt=@at,
                        AddedBy=@by,
                        IsActive=1,
                        Username=@un,
                        FirstName=@fn,
                        LastName=@ln;
                    ";

                    cmd.Parameters.AddWithValue("@uid", userId);
                    cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@by", addedBy);
                    cmd.Parameters.AddWithValue("@un", (object)username ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fn", (object)firstName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ln", (object)lastName ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeactivateAdmin(long userId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "UPDATE Admins SET IsActive=0 WHERE UserId=@uid;";
                    cmd.Parameters.AddWithValue("@uid", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

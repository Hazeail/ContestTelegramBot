using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ContestBot.Channels;

namespace ContestBot
{
    static partial class Database
    {
        public static List<ChannelInfo> LoadActiveChannels()
        {
            var result = new List<ChannelInfo>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "SELECT ChannelId, Username, Title, IsActive FROM Channels WHERE IsActive=1 ORDER BY Title, Username, ChannelId;";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var c = new ChannelInfo
                            {
                                ChannelId = reader.GetInt64(0),
                                Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Title = reader.IsDBNull(2) ? null : reader.GetString(2),
                                IsActive = !reader.IsDBNull(3) && reader.GetInt64(3) == 1
                            };
                            result.Add(c);
                        }
                    }
                }
            }

            return result;
        }

        public static void UpsertChannel(long channelId, string username, string title, long addedByAdminUserId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = @"
                    INSERT INTO Channels(ChannelId, Username, Title, IsActive, AddedByAdminUserId, AddedAtUtc)
                    VALUES(@cid, @un, @t, 1, @by, @at)
                    ON CONFLICT(ChannelId) DO UPDATE SET
                        Username=@un,
                        Title=@t,
                        IsActive=1,
                        AddedByAdminUserId=@by,
                        AddedAtUtc=@at;
                    ";

                    cmd.Parameters.AddWithValue("@cid", channelId);
                    cmd.Parameters.AddWithValue("@un", (object)username ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@t", (object)title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@by", addedByAdminUserId);
                    cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("o"));

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeactivateChannel(long channelId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "UPDATE Channels SET IsActive=0 WHERE ChannelId=@cid;";
                    cmd.Parameters.AddWithValue("@cid", channelId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
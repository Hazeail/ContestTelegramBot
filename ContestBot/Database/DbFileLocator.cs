using System;
using System.IO;

namespace ContestBot
{
    internal static class DbFileLocator
    {
        // Единое место хранения БД рядом с приложением: <base>/data/ContestBot.db
        internal static string GetStableDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var dataDir = Path.Combine(baseDir, "data");
            Directory.CreateDirectory(dataDir);

            return Path.Combine(dataDir, "ContestBot.db");
        }
    }
}
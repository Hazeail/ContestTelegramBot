using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace ContestBot
{
    static partial class Database
    {
        private static string _overrideDbPathForTests;

        private static bool _dbPathLogged;

        internal static void UseDbPathForTests(string dbPath)
        {
            _overrideDbPathForTests = dbPath;
        }

        internal static void ResetDbPathForTests()
        {
            _overrideDbPathForTests = null;
        }

        public static string ConnectionString
        {
            get
            {
                // Тесты имеют приоритет
                if (!string.IsNullOrWhiteSpace(_overrideDbPathForTests))
                    return "Data Source=" + _overrideDbPathForTests + ";Version=3;";

                var stablePath = DbFileLocator.GetStableDbPath();

                if (!_dbPathLogged)
                {
                    _dbPathLogged = true;
                    Console.WriteLine("[DB] Using SQLite file: " + stablePath);
                }

                return "Data Source=" + stablePath + ";Version=3;";
            }
        }
    }
}

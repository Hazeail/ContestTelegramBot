using System;
using System.IO;
using System.Text;

namespace ContestBot.Services
{
    internal sealed class CrashLogger
    {
        private static readonly object GlobalSync = new object();

        private readonly string _path;

        public CrashLogger(string fileName = "bot_crash.log")
        {
            _path = Path.Combine(AppContext.BaseDirectory, fileName);

            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
            }
            catch
            {
                // Ничего: логгер не должен валить бот
            }
        }

        public string PathToLog => _path;

        public void Info(string message) => Write("INFO", message, null);

        public void Warn(string message) => Write("WARN", message, null);

        public void Error(string message, Exception ex) => Write("ERROR", message, ex);

        public void Error(string message) => Write("ERROR", message, null);

        private void Write(string level, string message, Exception ex)
        {
            var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sb = new StringBuilder();

            sb.Append('[').Append(time).Append("] ").Append(level).Append(": ").AppendLine(message);

            if (ex != null)
                sb.AppendLine(ex.ToString());

            sb.AppendLine(new string('-', 90));

            var r = Rank(level);

            // 1) В консоль — по уровню
            if (r >= _minConsoleRank)
            {
                try
                {
                    Console.WriteLine($"[{time}] {level}: {message}");
                    if (ex != null) Console.WriteLine(ex);
                }
                catch { }
            }

            // 2) В файл — по уровню
            if (r >= _minFileRank)
            {
                try
                {
                    lock (GlobalSync)
                    {
                        File.AppendAllText(_path, sb.ToString(), Encoding.UTF8);
                    }
                }
                catch { }
            }
        }
        private static int _minConsoleRank = 1; // INFO
        private static int _minFileRank = 1;    // INFO

        public static void SetMinLevels(string consoleLevel, string fileLevel)
        {
            _minConsoleRank = Rank(consoleLevel);
            _minFileRank = Rank(fileLevel);
        }

        private static int Rank(string level)
        {
            level = (level ?? "").Trim().ToUpperInvariant();
            if (level == "ERROR") return 3;
            if (level == "WARN") return 2;
            return 1; // INFO
        }
    }
}

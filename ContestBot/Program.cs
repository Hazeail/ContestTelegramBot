using System;
using Microsoft.Extensions.Configuration;
using ContestBot.Config;

namespace ContestBot
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = config.GetSection("Bot").Get<BotSettings>();
            ContestBot.Services.CrashLogger.SetMinLevels(settings.ConsoleLogLevel, settings.FileLogLevel);

            if (settings == null)
                throw new Exception("Не удалось прочитать секцию Bot из appsettings.json");

            if (!string.IsNullOrWhiteSpace(settings.TokenFile))
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, settings.TokenFile);

                if (!System.IO.File.Exists(path))
                    throw new Exception("Не найден файл токена: " + path);

                settings.Token = System.IO.File.ReadAllText(path).Trim();
            }

            if (string.IsNullOrWhiteSpace(settings.Token))
                throw new Exception("Токен пустой. Укажи Bot:Token или Bot:TokenFile.");

            var bot = new BotService(settings);
            bot.Run();
        }
    }
}

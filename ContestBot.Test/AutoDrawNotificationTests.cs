using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Services;

namespace ContestBot.Test
{
    [TestClass]
    public partial class AutoDrawNotificationTests
    {
        [TestMethod]
        public async Task AutoDraw_SendsOnlyToOwner_WhenOwnerDeliveryOk()
        {

            // Arrange
            var contestManager = new ContestManager();
            ContestDrawService draw = null; // draw не нужен, т.к. тестируем ветку NotifyDrawingStuck
            long superAdminId = 9000;

            var timers = new ContestTimersService(contestManager, draw, superAdminId, _ => true);

            long ownerId = 5000;
            contestManager.SetContest(new Contest
            {
                Id = 1,
                Name = "T",
                Status = "Drawing",                 // ВАЖНО: чтобы AutoDrawTickDecision вернул NotifyDrawingStuck
                WinnersCount = 1,
                StartAt = DateTime.Now.AddHours(-1),
                EndAt = DateTime.Now.AddMinutes(-5), // ВАЖНО: stuckFor >= 2 минут
                CreatedByAdminUserId = ownerId
            });

            int toOwner = 0;
            int toSuper = 0;

            var bot = new FakeBotClient(async (chatId, text) =>
            {
                if (chatId == ownerId) toOwner++;
                if (chatId == superAdminId) toSuper++;
                await Task.CompletedTask;
            });

            // Act
            await timers.CheckContestAutoDrawAsync_ForTests(bot, CancellationToken.None);

            // Assert
            Assert.IsTrue(toOwner > 0, "Должно уйти владельцу (CreatedByAdminUserId).");
            Assert.AreEqual(0, toSuper, "Не должно уйти суперу, если владельцу доставилось.");
        }

        [TestMethod]
        public async Task AutoDraw_FallbacksToSuper_WhenOwnerDeliveryFails()
        {
            // Arrange
            var contestManager = new ContestManager();
            ContestDrawService draw = null; // draw не нужен, т.к. тестируем ветку NotifyDrawingStuck
            long superAdminId = 9000;

            var timers = new ContestTimersService(contestManager, draw, superAdminId, _ => true);

            long ownerId = 5000;
            contestManager.SetContest(new Contest
            {
                Id = 1,
                Name = "T",
                Status = "Drawing",                 // ВАЖНО: чтобы AutoDrawTickDecision вернул NotifyDrawingStuck
                WinnersCount = 1,
                StartAt = DateTime.Now.AddHours(-1),
                EndAt = DateTime.Now.AddMinutes(-5), // ВАЖНО: stuckFor >= 2 минут
                CreatedByAdminUserId = ownerId
            });

            int ownerAttempts = 0;
            int toSuper = 0;

            var bot = new FakeBotClient(async (chatId, text) =>
            {
                if (chatId == ownerId)
                {
                    ownerAttempts++;
                    throw new Exception("fail");
                }
                if (chatId == superAdminId)
                {
                    toSuper++;
                    await Task.CompletedTask;
                    return;
                }
                await Task.CompletedTask;
            });

            // Act
            await timers.CheckContestAutoDrawAsync_ForTests(bot, CancellationToken.None);

            // Assert
            Assert.IsTrue(ownerAttempts > 0, "Должна быть попытка отправить владельцу.");
            Assert.IsTrue(toSuper > 0, "Должен сработать fallback на супер-админа.");
        }

        // ---- ВАЖНО: draw нам не нужен, но TimersService требует объект.
        // Если у тебя ContestDrawService sealed и без интерфейса — в тесте можно передать реальный,
        // но чтобы не трогать БД/каналы — делаем минимальную заглушку, которая возвращает пустой список.
       
    }

}
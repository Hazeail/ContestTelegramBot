using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Admin.Creation;
using ContestBot.Admin.Manage;
using ContestBot.Callbacks.Actions;
using ContestBot.Channels;
using ContestBot.Services;
using TgUser = Telegram.Bot.Types.User;

namespace ContestBot.Tests
{
    [TestClass]
    public class AdminPublishTests
    {
        private string _dbFile;

        [TestInitialize]
        public void Init()
        {
            _dbFile = Path.Combine(Path.GetTempPath(), "contestbot_test_" + Guid.NewGuid().ToString("N") + ".db");
            ContestBot.Database.UseDbPathForTests(_dbFile);
            ContestBot.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ContestBot.Database.ResetDbPathForTests();
            try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
        }

        private static InlineKeyboardMarkup DummyKb()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("x", "x") }
            });
        }

        private static AdminCallbacksAction CreateAction(
            ContestCreationStore creationStore,
            ContestManageStore manageStore,
            Func<IReadOnlyList<ChannelInfo>, InlineKeyboardMarkup> buildPickChannelKb,
            Func<ITelegramBotClient, long, int, string, InlineKeyboardMarkup, CancellationToken, Task<int>> deleteAndSendHtmlAsync,
            Func<ITelegramBotClient, ContestBot.Contest, CancellationToken, Task> publishContestToChannelAsync,
            Func<ITelegramBotClient, long, long, int, string, CancellationToken, Task> showCreationPreviewAsync,
            Func<ITelegramBotClient, long, string, CancellationToken, Task> sendPrivateAsync = null)
        {
            Func<long, bool> isAdmin = _ => true;

            Func<int> getNextContestId = () => 1;

            Func<InlineKeyboardMarkup> kb = () => DummyKb();
            Func<int, InlineKeyboardMarkup> kbW = _ => DummyKb();

            Func<ITelegramBotClient, long, int, CancellationToken, Task> showAdminMenuAsync =
                (b, chatId, msgId, ct) => Task.CompletedTask;

            sendPrivateAsync = sendPrivateAsync ?? ((b, userId, txt, ct) => Task.CompletedTask);

            Func<ITelegramBotClient, ContestBot.Contest, CancellationToken, Task> tryUpdateChannelPostAsync =
                (b, c, ct) => Task.CompletedTask;

            Func<ITelegramBotClient, ContestBot.Contest, CancellationToken, Task<ChannelPostUpdateResult>> repostWithResultAsync =
                (b, c, ct) => Task.FromResult<ChannelPostUpdateResult>(null);

            long superAdminUserId = 999;

            return new AdminCallbacksAction(
                isAdmin,
                creationStore,
                getNextContestId,
                manageStore,

                buildAdminMenuKb: kb,
                buildAdminCreateTypeKb: kb,
                buildAdminCreateCancelKb: kb,
                buildAdminReferralPresetKb: kb,
                buildSkipMediaKb: kb,
                buildWinnersCountKb: kbW,
                buildAdminCreateChannelKb: buildPickChannelKb,

                buildAdminCreateEditMenuKb: kb,
                buildAdminEditTypeKb: kb,
                buildAdminEditReferralPresetKb: kb,

                showAdminMenuAsync: showAdminMenuAsync,
                deleteAndSendHtmlAsync: deleteAndSendHtmlAsync,
                publishContestToChannelAsync: publishContestToChannelAsync,
                sendPrivateAsync: sendPrivateAsync,
                tryUpdateChannelPostAsync: tryUpdateChannelPostAsync, repostChannelPostWithResultAsync: repostWithResultAsync,

                superAdminUserId: superAdminUserId,
                showCreationPreviewAsync: showCreationPreviewAsync
            );
        }

        [TestMethod]
        public async Task Publish_WithoutChannel_ShowsPickChannelScreen_AndDoesNotPublish()
        {
            // draft без ChannelId (но с именем)
            var draft = new ContestBot.Contest
            {
                Name = "Test contest",
                ChannelId = null
            };

            // store/session
            var creationStore = new ContestCreationStore();
            var manageStore = new ContestManageStore();

            var session = creationStore.GetOrCreate(123);
            session.Draft = draft;

            bool publishCalled = false;

            bool deleteAndSendHtmlCalled = false;
            int passedOldId = -1;
            string passedText = null;
            InlineKeyboardMarkup passedKb = null;

            var expectedPickKb = new InlineKeyboardMarkup(
                new[] { new[] { InlineKeyboardButton.WithCallbackData("pick", "pick") } });

            var action = CreateAction(
                creationStore,
                manageStore,

                buildPickChannelKb: channels =>
                {
                    // не важно, сколько каналов — важно, что использовали именно эту kb
                    return expectedPickKb;
                },

                deleteAndSendHtmlAsync: (bot, chatId, oldId, text, kb, ct) =>
                {
                    deleteAndSendHtmlCalled = true;
                    passedOldId = oldId;
                    passedText = text;
                    passedKb = kb;
                    Assert.Fail("Не должен вызываться HTML-экран на выборе канала.");
                    return Task.FromResult(0);
                },

                publishContestToChannelAsync: (bot, contest, ct) =>
                {
                    publishCalled = true;
                    return Task.CompletedTask;
                },

                showCreationPreviewAsync: (bot, chatId, adminId, msgId, header, ct) =>
                {
                    Assert.Fail("Не должно быть превью-ошибки, если просто не выбран канал.");
                    return Task.CompletedTask;
                }
            );

            session.PanelMessageId = 10;

            var cq = new CallbackQuery
            {
                Id = "cq1",
                Data = "admin:create_publish",
                From = new TgUser { Id = 123, Username = "admin" }
            };

            bool handled = await action.TryHandleAsync(null, cq, CancellationToken.None);

            Assert.IsTrue(handled, "admin:create_publish должен быть обработан.");

            Assert.IsTrue(deleteAndSendHtmlCalled, "Должен быть показан экран выбора канала через _deleteAndSendHtmlAsync.");
            Assert.AreEqual(10, passedOldId, "Должен редактироваться текущий msgId панели.");
            Assert.AreEqual("Перед публикацией выбери канал:", passedText);
            Assert.AreSame(expectedPickKb, passedKb, "Должна использоваться клавиатура из buildAdminCreateChannelKb(...)");

            Assert.IsFalse(publishCalled, "Публикация не должна происходить без выбранного канала.");
            Assert.AreEqual(777, session.PanelMessageId, "Должен сохраниться id нового сообщения панели.");
        }

        [TestMethod]
        public async Task Publish_ApiRequestException_ShowsPreviewError_AndDoesNotShowSuccess_AndDoesNotSendPrivate()
        {
            var draft = new ContestBot.Contest
            {
                Name = "Test contest",
                ChannelId = -1001234567890,
                ChannelUsername = "testchannel",
                EndAt = DateTime.UtcNow.AddHours(1),
                CreatedByAdminUserId = 555
            };

            var creationStore = new ContestCreationStore();
            var manageStore = new ContestManageStore();

            var session = creationStore.GetOrCreate(123);
            session.Draft = draft;

            bool publishCalled = false;

            bool showPreviewCalled = false;
            string previewHeader = null;

            bool deleteAndSendHtmlCalled = false;
            bool sendPrivateCalled = false;

            var action = CreateAction(
                creationStore,
                manageStore,

                buildPickChannelKb: channels => DummyKb(),

                deleteAndSendHtmlAsync: (bot, chatId, oldId, html, kb, ct) =>
                {
                    deleteAndSendHtmlCalled = true;
                    return Task.FromResult(777);
                },

                publishContestToChannelAsync: (bot, contest, ct) =>
                {
                    publishCalled = true;
                    throw new ApiRequestException("not enough rights");
                },

                sendPrivateAsync: (bot, userId, text, ct) =>
                {
                    sendPrivateCalled = true;
                    return Task.CompletedTask;
                },

                showCreationPreviewAsync: (bot, chatId, adminId, msgId, header, ct) =>
                {
                    showPreviewCalled = true;
                    previewHeader = header;
                    return Task.CompletedTask;
                }
            );

            session.PanelMessageId = 20;

            var cq = new CallbackQuery
            {
                Id = "cq2",
                Data = "admin:create_publish",
                From = new TgUser { Id = 123, Username = "admin" }
            };

            bool handled = await action.TryHandleAsync(null, cq, CancellationToken.None);

            Assert.IsTrue(handled, "admin:create_publish должен быть обработан.");
            Assert.IsTrue(publishCalled, "Должна быть попытка публикации (_publishContestToChannelAsync).");

            Assert.IsTrue(showPreviewCalled, "При ApiRequestException должен показываться превью с ошибкой.");
            Assert.IsNotNull(previewHeader);
            StringAssert.Contains(previewHeader, "Не получилось опубликовать конкурс");

            Assert.IsFalse(deleteAndSendHtmlCalled, "При ошибке публикации не должен показываться экран успеха (HTML delete&send).");
            Assert.IsFalse(sendPrivateCalled, "При ошибке публикации не должно быть отдельного личного уведомления (без спама).");

            Assert.AreEqual(20, session.PanelMessageId, "Панель должна остаться текущей (msgId), раз мы показали превью-ошибку.");
        }
    }
}
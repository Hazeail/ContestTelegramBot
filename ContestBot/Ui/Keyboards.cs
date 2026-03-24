using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;
using ContestBot.Admins;
using ContestBot.Channels;

namespace ContestBot.Ui
{
    internal sealed class Keyboards
    {
        public InlineKeyboardMarkup BuildAdminMenuKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🧩 Создание", "admin:home_create"),
                    InlineKeyboardButton.WithCallbackData("🏆 Конкурсы", "admin:home_contests")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚙️ Управление", "admin:home_manage"),
                    InlineKeyboardButton.WithCallbackData("❌ Закрыть", "admin:close")
                }
            });
        }


        public InlineKeyboardMarkup BuildAdminCreateTypeKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Реферальный", "admin:create_type:ref"),
                    InlineKeyboardButton.WithCallbackData("Обычный", "admin:create_type:norm")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:home_create")
                }
            });
        }

        public InlineKeyboardMarkup BuildAdminReferralPresetKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Мягкая", "admin:create_preset:1"),
                    InlineKeyboardButton.WithCallbackData("Стандарт", "admin:create_preset:2"),
                    InlineKeyboardButton.WithCallbackData("Агрессив", "admin:create_preset:3")
                },
               new[]
               {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_back_to_type")
               }
            });
        }

        public InlineKeyboardMarkup BuildAdminCreateCancelKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_back"),
                    InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                }
            });
        }

        public InlineKeyboardMarkup BuildPreviewKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Выбрать канал", "admin:create_pick_channel") },

                new[] { InlineKeyboardButton.WithCallbackData("Опубликовать", "admin:create_publish") },
                new[] { InlineKeyboardButton.WithCallbackData("Изменить", "admin:create_edit_menu") },

                new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "admin:create_cancel") }
            });
        }

        public InlineKeyboardMarkup BuildAdminCreateEditMenuKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Название", "admin:edit:name"),
                    InlineKeyboardButton.WithCallbackData("Описание", "admin:edit:desc")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Тип", "admin:edit:type"),
                    InlineKeyboardButton.WithCallbackData("Режим рефералов", "admin:edit:preset")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Медиа", "admin:edit:media"),
                    InlineKeyboardButton.WithCallbackData("Призовые", "admin:edit:winners")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Дата", "admin:edit:date"),
                    InlineKeyboardButton.WithCallbackData("Канал", "admin:create_pick_channel")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_preview")
                }
            });
        }

        public InlineKeyboardMarkup BuildAdminEditTypeKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Обычный", "admin:edit:type:norm"),
                    InlineKeyboardButton.WithCallbackData("Реферальный", "admin:edit:type:ref")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_back")
                }
            });
        }

        public InlineKeyboardMarkup BuildAdminEditReferralPresetKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Режим 1", "admin:edit:preset:1"),
                    InlineKeyboardButton.WithCallbackData("Режим 2", "admin:edit:preset:2"),
                    InlineKeyboardButton.WithCallbackData("Режим 3", "admin:edit:preset:3")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_back")
                }
            });
        }

        public InlineKeyboardMarkup BuildSkipMediaKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("Без медиа", "admin:create_skip_media") },
                new [] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_back" ) }
            });
        }

        public InlineKeyboardMarkup BuildWinnersCountKeyboard(int value)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➖", "admin:winners:dec"),
                    InlineKeyboardButton.WithCallbackData(value.ToString(), "admin:winners:noop"),
                    InlineKeyboardButton.WithCallbackData("➕", "admin:winners:inc")
                },
                new[] { InlineKeyboardButton.WithCallbackData("✅ OK", "admin:winners:ok") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_back"),
                    InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                }
            });
        }

        public InlineKeyboardMarkup BuildAdminAdminsKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Добавить", "admin:admins_add"),
                    InlineKeyboardButton.WithCallbackData("Отключить", "admin:admins_off")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:home_manage"),
                    InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                },
            });
        }

        public InlineKeyboardMarkup BuildAdminAdminsWaitKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:admins_cancel") },
                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
            });
        }

        public InlineKeyboardMarkup BuildAdminAdminsDisablePickKeyboard(IReadOnlyList<AdminProfile> admins)
        {
            var rows = new List<InlineKeyboardButton[]>();

            if (admins != null)
            {
                foreach (var a in admins)
                {
                    string title = a == null ? "" : a.GetDisplayName();
                    if (string.IsNullOrWhiteSpace(title)) title = a?.UserId.ToString() ?? "";

                    rows.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Отключить " + title, "admin:admins_disable:" + (a?.UserId ?? 0))
                    });
                }
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:admins") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") });
            return new InlineKeyboardMarkup(rows);
        }

        // --- МЕТОДЫ КАНАЛОВ ---
        public InlineKeyboardMarkup BuildAdminChannelsKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Добавить", "admin:channels_add"),
                    InlineKeyboardButton.WithCallbackData("Отключить", "admin:channels_off")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:home_manage"),
                    InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
                },
            });
        }

        public InlineKeyboardMarkup BuildAdminChannelsWaitKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:channels_cancel") },
                new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") }
            });
        }
       
        public InlineKeyboardMarkup BuildAdminChannelsDisablePickKeyboard(IReadOnlyList<ChannelInfo> channels)
        {
            var rows = new List<InlineKeyboardButton[]>();

            if (channels != null)
            {
                foreach (var c in channels)
                {
                    string title = c == null ? "" : c.GetDisplayName();
                    if (string.IsNullOrWhiteSpace(title)) title = c?.ChannelId.ToString() ?? "";

                    rows.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Отключить " + title, "admin:channels_disable:" + (c?.ChannelId ?? 0))
                    });
                }
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:channels") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu") });
            return new InlineKeyboardMarkup(rows);
        }

        public InlineKeyboardMarkup BuildAdminCreateChannelKeyboard(IReadOnlyList<ChannelInfo> channels)
        {
            var rows = new List<InlineKeyboardButton[]>();

            if (channels != null)
            {
                foreach (var c in channels)
                {
                    var title = c?.GetDisplayName();
                    if (string.IsNullOrWhiteSpace(title))
                        title = c?.ChannelId.ToString() ?? "Канал";

                    rows.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData(title, "admin:create_channel:" + (c?.ChannelId ?? 0))
                    });
                }
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:create_preview") });

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("↩️ Админ-панель", "admin:menu")
            });

            return new InlineKeyboardMarkup(rows);
        }
    }
}

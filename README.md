# Конкурсный Telegram-бот (C# / .NET Framework 4.7.2)

Проект: Telegram-бот с админ-панелью (inline UI), конкурсами/рефералами и хранением данных в SQLite.

## Стек
- C# / .NET Framework 4.7.2 (net472)
- Telegram.Bot
- SQLite (System.Data.SQLite)
- Entity Framework 6
- MSTest

## Быстрый старт
1. Установи Visual Studio 2019/2022 с workload **.NET desktop development**.
2. В папке `ContestBot/`:
   - Скопируй `appsettings.example.json` → `appsettings.json`
   - Создай `bot_token.txt` и вставь туда токен Telegram-бота (одна строка).
3. Открой `ContestBot.csproj` и `ContestBot.Test.csproj`, восстанови NuGet-пакеты (Restore).
4. Запусти `ContestBot`.

## Что не коммитится
- `bin/`, `obj/`
- `ContestBot/appsettings.json`, `bot_token.txt`
- runtime-данные (`*.db`, `ContestBot/data/*`) и логи

> Код проекта (как продукт) публикуется без секретов и runtime-артефактов.

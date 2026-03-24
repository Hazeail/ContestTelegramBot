using System;
using System.Linq;
using System.Collections.Generic;


namespace ContestBot
{
    internal class ContestManager
    {
        // Храним все конкурсы по Id
        private readonly Dictionary<int, Contest> _contestsById = new Dictionary<int, Contest>();

        // Id текущего "активного" конкурса для всех методов GetCurrentContest/HasActiveContest и т.п.
        private int? _currentContestId;

        // Храним конкурсы ещё и по коду (c1, c2, ...)
        private readonly Dictionary<string, Contest> _contestsByCode =
            new Dictionary<string, Contest>(StringComparer.OrdinalIgnoreCase);

        private Contest RefreshContestById(int id)
        {
            // Достаём свежую версию из БД
            var db = Database.LoadContestById(id);

            if (db == null)
            {
                // Конкурс удалён из БД — чистим кеш
                Contest old;
                if (_contestsById.TryGetValue(id, out old) && old != null && !string.IsNullOrWhiteSpace(old.Code))
                    _contestsByCode.Remove(old.Code);

                _contestsById.Remove(id);

                if (_currentContestId.HasValue && _currentContestId.Value == id)
                    _currentContestId = null;

                return null;
            }

            // Гарантируем Code (как у тебя в конструкторе менеджера)
            if (string.IsNullOrWhiteSpace(db.Code))
            {
                db.Code = "c" + db.Id;
                Database.SaveContest(db);
            }

            // Если код поменялся — удалим старую привязку
            Contest cached;
            if (_contestsById.TryGetValue(id, out cached) && cached != null)
            {
                if (!string.IsNullOrWhiteSpace(cached.Code) &&
                    !string.Equals(cached.Code, db.Code, StringComparison.OrdinalIgnoreCase))
                {
                    _contestsByCode.Remove(cached.Code);
                }
            }

            _contestsById[db.Id] = db;
            _contestsByCode[db.Code] = db;

            return db;
        }

        // Конструктор менеджера конкурсов
        public ContestManager()
        {
            Database.EnsureCreated();
            var contests = Database.LoadAllContests();

            if (contests.Count > 0)
            {
                // Кладём все конкурсы в словарь
                foreach (var c in contests)
                {
                    if (string.IsNullOrWhiteSpace(c.Code))
                    {
                        c.Code = "c" + c.Id;
                        Database.SaveContest(c);
                    }

                    _contestsById[c.Id] = c;
                    _contestsByCode[c.Code] = c;
                }

                // Выбираем текущий:
                // 1) если есть Running — берём первый Running
                // 2) иначе — берём самый "последний" по Id (у нас LoadAllContests сортирует по Id DESC)
                Contest current = null;

                foreach (var c in contests)
                {
                    if (c.Status == "Running")
                    {
                        current = c;
                        break;
                    }
                }

                if (current == null)
                    current = contests[0];

                _currentContestId = current.Id;

                Console.WriteLine(
                    "[CONTEST] Загружено конкурсов: {0}. Текущий: '{1}' (Id={2}, Status={3})",
                    contests.Count,
                    current.Name,
                    current.Id,
                    current.Status
                );
            }
            else
            {
                Console.WriteLine("[CONTEST] В БД нет сохранённых конкурсов.");
            }
        }

        // Удобное свойство, возвращающее текущий конкурс (если он выбран)
        private Contest CurrentContest
        {
            get
            {
                if (!_currentContestId.HasValue)
                    return null;

                return RefreshContestById(_currentContestId.Value);
            }
        }

        // Устанавливаем/обновляем текущий конкурс
        public void SetContest(Contest contest)
        {
            if (contest == null)
                return;

            if (string.IsNullOrWhiteSpace(contest.Code))
                contest.Code = "c" + contest.Id;

            _contestsById[contest.Id] = contest;
            _contestsByCode[contest.Code] = contest;
            _currentContestId = contest.Id;

            Database.SaveContest(contest);

            Console.WriteLine(
                "[CONTEST] Текущий конкурс установлен: '{0}' (Id={1}, Status={2})",
                contest.Name,
                contest.Id,
                contest.Status
            );
        }

        public void SetContestDates(DateTime start, DateTime end)
        {
            var contest = CurrentContest;
            if (contest == null)
                return;

            contest.StartAt = start;
            contest.EndAt = end;

            Console.WriteLine(
                $"Даты конкурса '{contest.Name}' обновлены. " +
                $"Старт: {start}, Финиш: {end}"
            );

            Database.SaveContest(contest);
        }

        public bool HasActiveContest
        {
            get
            {
                var c = CurrentContest;
                return c != null && (c.Status == "Running" || c.Status == "Drawing");
            }
        }

        public Contest GetActiveContest()
        {
            var c = CurrentContest;
            return (c != null && (c.Status == "Running" || c.Status == "Drawing")) ? c : null;
        }

        public Contest GetCurrentContest()
        {
            return CurrentContest;
        }

        public bool TrySetCurrentContest(int contestId)
        {
            Contest c;
            if (!_contestsById.TryGetValue(contestId, out c))
                return false;

            _currentContestId = contestId;

            Console.WriteLine("[CONTEST] Текущий конкурс выбран: '{0}' (Id={1}, Status={2})", c.Name, c.Id, c.Status);
            return true;
        }

        public bool IsRunningNow()
        {
            var c = CurrentContest;
            if (c == null)
                return false;

            return c.Status == "Running" && DateTime.Now <= c.EndAt;
        }

        public void StartContest()
        {
            var c = CurrentContest;
            if (c == null)
                return;

            c.Status = "Running";

            Database.SaveContest(c);

            Console.WriteLine(
                $"[CONTEST] Старт конкурса '{c.Name}' " +
                $"(Id={c.Id}) " +
                $"Now={DateTime.Now:dd.MM.yyyy HH:mm:ss}, " +
                $"StartAt={c.StartAt:dd.MM.yyyy HH:mm}, " +
                $"EndAt={c.EndAt:dd.MM.yyyy HH:mm}"
            );
        }

        public void FinishContest()
        {
            var c = CurrentContest;
            if (c == null)
                return;

            c.Status = "Finished";

            Database.SaveContest(c);

            Console.WriteLine($"Конкурс '{c.Name}' завершён.");
        }

        public string GetContestInfoText()
        {
            var c = CurrentContest;
            if (c == null)
                return "Конкурс не настроен.";

            // Определяем режим реферальной системы
            string referralModeDescription;

            if (c.Type == "referral")
            {
                // Пытаемся распознать пресет по параметрам
                if (Math.Abs(c.PerReferralWeight - 0.2) < 0.0001 &&
                    Math.Abs(c.MaxWeight - 3) < 0.0001)
                {
                    referralModeDescription = "мягкая рефералька";
                }
                else if (Math.Abs(c.PerReferralWeight - 0.3) < 0.0001 &&
                         Math.Abs(c.MaxWeight - 7.5) < 0.0001)
                {
                    referralModeDescription = "стандартная рефералька";
                }
                else if (Math.Abs(c.PerReferralWeight - 0.5) < 0.0001 &&
                         Math.Abs(c.MaxWeight - 10) < 0.0001)
                {
                    referralModeDescription = "агрессивная рефералька";
                }
                else
                {
                    referralModeDescription = "кастомные настройки реферального конкурса";
                }
            }
            else
            {
                referralModeDescription = "реферальная система отключена (равные шансы для всех участников)";
            }

            return
                $"Конкурс: {c.Name}\n" +
                $"Тип: {c.Type}\n" +
                $"Статус: {c.Status}\n" +
                $"Старт: {c.StartAt}\n" +
                $"Финиш: {c.EndAt}\n" +
                $"\nРежим реферальной системы: {referralModeDescription}\n" +
                $"\nБазовый вес: {c.BaseWeight}\n" +
                $"Вес за реферала: {c.PerReferralWeight}\n" +
                $"Максимальный вес: {c.MaxWeight}";
        }

        public Contest GetContestById(int id)
        {
            if (id <= 0) return null;
            return RefreshContestById(id);
        }

        public Contest GetContestByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            Contest c;
            if (_contestsByCode.TryGetValue(code, out c) && c != null)
            {
                // Обновляем по Id, чтобы получить актуальный Type/Status/веса/и т.д.
                return RefreshContestById(c.Id);
            }

            // Фолбэк: если кода нет в кеше, попробуем найти в БД (редко, но полезно)
            var all = Database.LoadAllContests();
            var fromDb = all.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            if (fromDb == null)
                return null;

            _contestsById[fromDb.Id] = fromDb;
            if (!string.IsNullOrWhiteSpace(fromDb.Code))
                _contestsByCode[fromDb.Code] = fromDb;

            return fromDb;
        }

    }
}

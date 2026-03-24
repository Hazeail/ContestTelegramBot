using System.Collections.Generic;

namespace ContestBot.Admin.Manage
{
    internal sealed class ContestManageStore
    {
        internal sealed class Session
        {
            public ContestManageState State { get; set; } = ContestManageState.None;
            public int? ContestId { get; set; }
            public int? PanelMessageId { get; set; }

            public bool IsActive => State != ContestManageState.None && ContestId.HasValue;

            public void Reset()
            {
                State = ContestManageState.None;
                ContestId = null;
                // PanelMessageId оставляем как якорь, чтобы продолжать редактировать панель
            }
        }

        private readonly Dictionary<long, Session> _sessions = new Dictionary<long, Session>();

        public Session GetOrCreate(long adminId)
        {
            if (!_sessions.TryGetValue(adminId, out var s))
            {
                s = new Session();
                _sessions[adminId] = s;
            }
            return s;
        }

        public Session TryGet(long adminId)
        {
            _sessions.TryGetValue(adminId, out var s);
            return s;
        }
    }
}
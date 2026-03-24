using System.Collections.Generic;

namespace ContestBot.Admin.Creation
{
    internal sealed class ContestCreationStore
    {
        internal sealed class Session
        {
            public ContestCreationState State { get; set; } = ContestCreationState.None;
            public Contest Draft { get; set; }
            public int? PanelMessageId { get; set; }
            public long? DraftId { get; set; }

            public bool IsActive => State != ContestCreationState.None;

            public void Reset()
            {
                State = ContestCreationState.None;
                Draft = null;
                PanelMessageId = null;
                DraftId = null;
            }
        }

        private readonly Dictionary<long, Session> _sessions = new Dictionary<long, Session>();

        public Session GetOrCreate(long adminUserId)
        {
            if (!_sessions.TryGetValue(adminUserId, out var s))
            {
                s = new Session();
                _sessions[adminUserId] = s;
            }
            return s;
        }

        public Session TryGet(long adminUserId)
        {
            _sessions.TryGetValue(adminUserId, out var s);
            return s;
        }

        public void Reset(long adminUserId)
        {
            if (_sessions.TryGetValue(adminUserId, out var s))
                s.Reset();
        }
    }
}
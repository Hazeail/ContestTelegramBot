using System;

namespace ContestBot.Admins
{
    internal sealed class AdminDirectory
    {
        private readonly long _superAdminUserId;

        public AdminDirectory(long superAdminUserId)
        {
            _superAdminUserId = superAdminUserId;
        }

        public long SuperAdminUserId => _superAdminUserId;

        public bool IsSuperAdmin(long userId) => userId == _superAdminUserId;

        public bool IsAdmin(long userId)
        {
            if (IsSuperAdmin(userId)) return true;

            try
            {
                return Database.IsActiveAdmin(userId);
            }
            catch
            {
                // если БД упала — хотя бы супер-админ остаётся с доступом
                return false;
            }
        }
    }
}
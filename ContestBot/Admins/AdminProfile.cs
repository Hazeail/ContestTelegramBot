namespace ContestBot.Admins
{
    internal sealed class AdminProfile
    {
        public long UserId { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(Username))
                return "@" + Username.Trim().TrimStart('@');

            var full = ((FirstName ?? "").Trim() + " " + (LastName ?? "").Trim()).Trim();
            if (!string.IsNullOrWhiteSpace(full))
                return full;

            // last resort
            return UserId.ToString();
        }
    }
}

namespace HBCDirectory.Models
{
    /// <summary>
    /// Login credentials for a church member.
    /// Created automatically when the admin adds a member.
    /// Username is always the member's email address.
    /// Password is always stored as a BCrypt hash.
    /// </summary>
    public class MemberAccount
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;

        // Always the member's email, set automatically from Member.Email
        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;
    }
}

namespace HBCDirectory.Models
{
    public class PasswordResetToken
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }

        // Only set for admin password resets.
        // Since the admin password lives in config (not the DB), we store
        // the new BCrypt hash here and Login checks it as an override.
        public string? NewPasswordHash { get; set; }
    }
}

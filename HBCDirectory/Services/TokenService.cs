using HBCDirectory.Data;
using HBCDirectory.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HBCDirectory.Services
{
    public class TokenService
    {
        private readonly DirectoryContext _db;

        public TokenService(DirectoryContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Generates a cryptographically random, URL-safe token for the given email
        /// and saves it to the database.
        /// </summary>
        public async Task<string> CreateTokenAsync(string email, TimeSpan expiry)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Email     = email.ToLower(),
                Token     = token,
                ExpiresAt = DateTime.UtcNow.Add(expiry),
                Used      = false
            });

            await _db.SaveChangesAsync();
            return token;
        }

        /// <summary>
        /// Validates a token. Returns the token record if valid, null if not found,
        /// already used, or expired.
        /// </summary>
        public async Task<PasswordResetToken?> ValidateTokenAsync(string token)
        {
            return await _db.PasswordResetTokens
                .FirstOrDefaultAsync(t =>
                    t.Token == token &&
                    !t.Used &&
                    t.ExpiresAt > DateTime.UtcNow);
        }

        /// <summary>
        /// Marks a token as used so it cannot be reused.
        /// </summary>
        public async Task MarkUsedAsync(PasswordResetToken token)
        {
            token.Used = true;
            await _db.SaveChangesAsync();
        }
    }
}

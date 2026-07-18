using HBCDirectory.Data;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly IConfiguration _config;
        private readonly TokenService _tokens;

        public ResetPasswordModel(
            DirectoryContext db,
            IConfiguration config,
            TokenService tokens)
        {
            _db     = db;
            _config = config;
            _tokens = tokens;
        }

        public string? TokenError { get; set; }
        public bool Success { get; set; }
        public string? Token { get; set; }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            Token = token;

            var record = await _tokens.ValidateTokenAsync(token);
            if (record == null)
                TokenError = "This reset link is invalid or has expired. Please request a new one.";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            string token, string newPassword, string confirmPassword)
        {
            Token = token;

            //  Basic validation 
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return Page();
            }

            if (newPassword.Length < 8)
            {
                ModelState.AddModelError("", "Password must be at least 8 characters.");
                return Page();
            }

            //  Validate token 
            var record = await _tokens.ValidateTokenAsync(token);
            if (record == null)
            {
                TokenError = "This reset link is invalid or has expired.";
                return Page();
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            //  Apply to member account 
            var account = await _db.MemberAccounts
                .FirstOrDefaultAsync(a => a.Username == record.Email);

            if (account != null)
            {
                account.PasswordHash = newHash;
                await _db.SaveChangesAsync();
            }
            else
            {
                // It's the admin — check email matches AdminCredentials:Email
                var adminEmail = _config["AdminCredentials:Email"] ?? string.Empty;
                if (!string.Equals(record.Email, adminEmail.ToLower(), StringComparison.OrdinalIgnoreCase))
                {
                    TokenError = "This reset link is no longer valid.";
                    return Page();
                }

                // Store the new hash on the token record.
                // Login.cshtml.cs checks this when the admin logs in.
                record.NewPasswordHash = newHash;
                await _db.SaveChangesAsync();
            }

            //  Mark token as used 
            await _tokens.MarkUsedAsync(record);

            Success = true;
            return Page();
        }
    }
}

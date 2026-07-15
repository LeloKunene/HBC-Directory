using HBCDirectory.Data;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly DirectoryContext _db;
        private readonly EmailService _email;
        private readonly TokenService _tokens;

        public ForgotPasswordModel(
            IConfiguration config, DirectoryContext db,
            EmailService email, TokenService tokens)
        {
            _config = config;
            _db     = db;
            _email  = email;
            _tokens = tokens;
        }

        // Always true after submit — never reveal if the email exists (prevents enumeration)
        public bool Sent { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(string email)
        {
            Sent = true; // Always show success regardless of outcome

            if (string.IsNullOrWhiteSpace(email)) return Page();

            var input = email.Trim().ToLower();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            // ── Check if it's a member ─────────────────────────────────────────
            var member = await _db.Members
                .Include(m => m.Family)
                .FirstOrDefaultAsync(m => m.Email == input);

            if (member != null)
            {
                var token     = await _tokens.CreateTokenAsync(input, TimeSpan.FromHours(1));
                var resetLink = $"{baseUrl}/ResetPassword?token={token}";

                await _email.SendPasswordResetEmailAsync(
                    toEmail:            member.Email,
                    memberName:         $"{member.Name} {member.Surname}",
                    resetPasswordLink:  resetLink
                );

                return Page();
            }

            // ── Check if it's the admin ────────────────────────────────────────
            var adminEmail = _config["AdminCredentials:Email"];
            if (!string.IsNullOrEmpty(adminEmail) &&
                input == adminEmail.ToLower())
            {
                var token     = await _tokens.CreateTokenAsync(input, TimeSpan.FromHours(1));
                var resetLink = $"{baseUrl}/ResetPassword?token={token}";

                await _email.SendPasswordResetEmailAsync(
                    toEmail:           adminEmail,
                    memberName:        "Admin",
                    resetPasswordLink: resetLink
                );
            }

            return Page();
        }
    }
}

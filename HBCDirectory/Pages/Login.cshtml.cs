using HBCDirectory.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HBCDirectory.Pages
{
    [EnableRateLimiting("login")]
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly DirectoryContext _db;

        public LoginModel(IConfiguration config, DirectoryContext db)
        {
            _config = config;
            _db = db;
        }

        public string? ErrorMessage { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter your email and password.";
                return Page();
            }

            var input = username.Trim().ToLower();

            //  1. Check admin credentials 
            var adminUser = _config["AdminCredentials:Username"] ?? "admin";
            var adminPass = _config["AdminCredentials:Password"]
                ?? throw new InvalidOperationException("AdminCredentials:Password not configured.");

            if (input == adminUser && password == adminPass)
            {
                await SignInAsync(input, new[] { "Admin", "SystemAdmin" });
                return RedirectToPage("/Admin");
            }

            //  2. Check member accounts (username = email) 
            var account = await _db.MemberAccounts
                .Include(a => a.Member)
                .FirstOrDefaultAsync(a => a.Username == input);

            if (account != null && BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
            {
                var grantedRoles = await _db.MemberRoles
                    .Where(mr => mr.MemberId == account.MemberId)
                    .Select(mr => mr.Role.Name)
                    .ToListAsync();

                var roles = new List<string> { "Member" };
                roles.AddRange(grantedRoles);

                await SignInAsync(input, roles, account.MemberId.ToString());
                return RedirectToPage("/Index");
            }

            //  3. Both failed 
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        private async Task SignInAsync(string username, IEnumerable<string> roles, string? memberId = null)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            if (memberId != null)
                claims.Add(new Claim("MemberId", memberId));

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}

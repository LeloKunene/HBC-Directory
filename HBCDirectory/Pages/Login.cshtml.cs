using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HBCDirectory.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;

        public LoginModel(IConfiguration config)
        {
            _config = config;
        }

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(string username, string password)
        {
            var adminUser = _config["AdminCredentials:Username"]
                ?? throw new InvalidOperationException("AdminCredentials:Username is not configured.");
            var adminPass = _config["AdminCredentials:Password"]
                ?? throw new InvalidOperationException("AdminCredentials:Password is not configured.");

            if (username == adminUser && password == adminPass)
            {
                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return RedirectToPage("/Admin");
            }

            ErrorMessage = "Invalid credentials";
            return Page();
        }
    }
}
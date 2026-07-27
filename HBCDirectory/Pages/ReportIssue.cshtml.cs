using HBCDirectory.Data;
using HBCDirectory.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HBCDirectory.Pages
{
    public class ReportIssueModel : PageModel
    {
        private readonly DirectoryContext _db;

        public ReportIssueModel(DirectoryContext db) => _db = db;

        public IActionResult OnGet() => RedirectToPage("/Index");

        public async Task<IActionResult> OnPostAsync(
            string category, string description, string? pageUrl, string? userAgent, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                TempData["Error"] = "Describe what happened before sending.";
                return SafeRedirect(returnUrl);
            }

            _db.IssueReports.Add(new IssueReport
            {
                Category           = category is "Bug" or "Suggestion" or "Other" ? category : "Other",
                Description        = description.Trim(),
                PageUrl            = pageUrl,
                UserAgent          = userAgent,
                ReportedByMemberId = GetMemberId()
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Thanks — your report was sent.";
            return SafeRedirect(returnUrl);
        }

        private IActionResult SafeRedirect(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToPage("/Index");

        private int? GetMemberId()
        {
            var claim = User.FindFirst("MemberId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}

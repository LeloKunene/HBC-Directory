using HBCDirectory.Data;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages.Directory
{
    [Authorize]
    [EnableRateLimiting("pdf")]
    public class DownloadModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly DirectoryPdfService _pdfService;

        public DownloadModel(DirectoryContext db, DirectoryPdfService pdfService)
        {
            _db = db;
            _pdfService = pdfService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var settings = await _db.PdfSettings.FindAsync(1);

            // Prefer the cached PDF already generated via "Update PDF" in Admin —
            // this is what respects the saved page order/selection and password.
            if (settings?.R2Key != null)
            {
                var cached = await _pdfService.DownloadFromR2Async(settings.R2Key);
                if (cached != null)
                {
                    var cachedFileName = $"HBC-Directory-{DateTime.Today:yyyy-MM-dd}.pdf";
                    return File(cached, "application/pdf", cachedFileName);
                }
            }

            // Fallback: nothing generated yet — build one on the fly using
            // whatever page settings are saved (or defaults if none).
            var families = await _db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .ToListAsync();

            var unassigned = await _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname)
                .ThenBy(m => m.Name)
                .ToListAsync();

            var pages = settings?.GetPages();
            var pdfBytes = await _pdfService.GenerateAsync(families, unassigned, pages);

            if (settings?.HasPassword == true)
                pdfBytes = HBCDirectory.Pages.AdminModel.PdfPasswordHelper.AddPassword(pdfBytes, settings.Password!);

            var fileName = $"HBC-Directory-{DateTime.Today:yyyy-MM-dd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
using HBCDirectory.Data;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages.Directory
{
    [Authorize]
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
            // Families with their members (children included under the family)
            var families = await _db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .ToListAsync();

            // Individual members — adults only, no family assigned.
            // Children always appear under a family; if a child record has
            // FamilyId == null that is a data error, not an individual member.
            var unassigned = await _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname)
                .ThenBy(m => m.Name)
                .ToListAsync();

            var pdfBytes = await _pdfService.GenerateAsync(families, unassigned);
            var fileName = $"HBC-Directory-{DateTime.Today:yyyy-MM-dd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
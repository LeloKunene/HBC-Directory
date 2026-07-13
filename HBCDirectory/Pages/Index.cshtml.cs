using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    public class IndexModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly PhotoService _photos;
        private readonly IConfiguration _config;

        public IndexModel(DirectoryContext db, IConfiguration config, PhotoService photos)
        {
            _db = db;
            _config = config;
            _photos = photos;
        }
        public string PhotoUrl(string? fileName) => _photos.Url(fileName);

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? familyId { get; set; }

        public List<Family> Families { get; set; } = new();
        public List<Member> Members { get; set; } = new();

        public async Task OnGetAsync()
        {
            var membersQuery = _db.Members.Include(m => m.Family).AsQueryable();
            if (familyId.HasValue)
            {
                membersQuery = membersQuery.Where(m => m.FamilyId == familyId.Value);
            }
            if (!string.IsNullOrEmpty(q))
            {
                membersQuery = membersQuery.Where(m => m.Name.Contains(q) || m.Surname.Contains(q));
            }

            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await membersQuery.OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();
        }

        public bool IsUpcoming(DateTime? date)
        {
            if (!date.HasValue) return false;
            var today = DateTime.Today;
            var thisYear = new DateTime(today.Year, date.Value.Month, date.Value.Day);
            var diff = (thisYear - today).TotalDays;
            // Handle year wrap (e.g., birthday is Jan 5, today is Dec 28)
            if (diff < 0) diff = (thisYear.AddYears(1) - today).TotalDays;
            return diff >= 0 && diff <= 14; // within the next 14 days
        }
    }
}

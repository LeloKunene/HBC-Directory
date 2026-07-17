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

        public IndexModel(DirectoryContext db, PhotoService photos)
        {
            _db = db;
            _photos = photos;
        }
        public string PhotoUrl(string? fileName) => _photos.Url(fileName);

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? familyId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? role { get; set; }
        
        public List<Family> Families { get; set; } = new();
        public List<Member> Members { get; set; } = new();

        public List<Member> UpcomingBirthdays { get; set; } = new();
        public List<Member> UpcomingAnniversaries { get; set; } = new();

        public async Task OnGetAsync()
        {
            var query = _db.Members.Include(m => m.Family).AsQueryable();

            if (!string.IsNullOrEmpty(q))
                query = query.Where(m => m.Name.Contains(q) || m.Surname.Contains(q));

            if (familyId.HasValue)
                query = query.Where(m => m.FamilyId == familyId.Value);

            if (!string.IsNullOrEmpty(role))
                query = query.Where(m => m.Role == role);

            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await query.OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();

            var allMembers = await _db.Members.OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();

            UpcomingBirthdays = allMembers
                .Where(m => IsWithinDays(m.Birthdate, 30))
                .OrderBy(m => NextOccurrence(m.Birthdate!.Value))
                .ToList();

            UpcomingAnniversaries = allMembers
                .Where(m => IsWithinDays(m.Anniversary, 30))
                .OrderBy(m => NextOccurrence(m.Anniversary!.Value))
                .ToList();
        }

        private static bool IsWithinDays(DateTime? date, int days)
        {
            if (!date.HasValue) return false;
            var today = DateTime.Today;
            var thisYear = new DateTime(today.Year, date.Value.Month, date.Value.Day);
            if (thisYear < today) thisYear = thisYear.AddYears(1);
            return (thisYear - today).TotalDays <= days;
        }

        private static DateTime NextOccurrence(DateTime date)
        {
            var today = DateTime.Today;
            var next = new DateTime(today.Year, date.Month, date.Day);
            if (next < today) next = next.AddYears(1);
            return next;
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

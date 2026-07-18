using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    [Authorize]  // Login required — not public
    public class IndexModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly PhotoService _photos;

        public IndexModel(DirectoryContext db, PhotoService photos)
        {
            _db = db;
            _photos = photos;
        }

        //  Data for the directory 
        // Leadership — Elders and Deacons featured at the top
        public List<Member> Leadership { get; set; } = new();

        // Staff assignments for the staff section
        public List<StaffAssignment> StaffAssignments { get; set; } = new();

        // All families (sorted alphabetically)
        public List<Family> Families { get; set; } = new();

        // Individual members — adults not in any family
        public List<Member> IndividualMembers { get; set; } = new();

        // Search/filter params
        public string? Q         { get; set; }
        public string? StatusFilter { get; set; }
        public string? OfficeFilter { get; set; }

        public string PhotoUrl(string? f) => _photos.Url(f);

        public List<Member> UpcomingBirthdays    { get; set; } = new();
        public List<Member> UpcomingAnniversaries { get; set; } = new();

        public async Task OnGetAsync(string? q, string? status, string? office)
        {
            Q            = q?.Trim();
            StatusFilter = status;
            OfficeFilter = office;

            //  Leadership 
            Leadership = await _db.Members
                .Include(m => m.Family)
                .Where(m => m.ChurchOffice == "Elder" || m.ChurchOffice == "Deacon")
                .OrderBy(m => m.ChurchOffice).ThenBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

            //  Staff 
            StaffAssignments = await _db.StaffAssignments
                .Include(sa => sa.Member).ThenInclude(m => m.Family)
                .Include(sa => sa.StaffRole)
                .OrderBy(sa => sa.DisplayOrder)
                .ToListAsync();

            //  Families (alphabetical) 
            var familiesQ = _db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .AsQueryable();

            Families = await familiesQ.ToListAsync();

            //  Individual members (adults with no family) 
            var individualsQ = _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .AsQueryable();

            if (!string.IsNullOrEmpty(StatusFilter))
                individualsQ = individualsQ.Where(m => m.MemberStatus == StatusFilter);
            if (!string.IsNullOrEmpty(OfficeFilter))
                individualsQ = individualsQ.Where(m => m.ChurchOffice == OfficeFilter);

            IndividualMembers = await individualsQ.ToListAsync();

            var today    = DateTime.Today;
            var in30days = today.AddDays(30);

            var allMembers = await _db.Members.ToListAsync();

            UpcomingBirthdays = allMembers
                .Where(m => m.Birthdate.HasValue && m.ShowBirthdate)
                .Where(m =>
                {
                    var bd = m.Birthdate!.Value;
                    try { var t = new DateTime(today.Year, bd.Month, bd.Day); return t >= today && t <= in30days; }
                    catch { return false; }
                })
                .OrderBy(m => m.Birthdate!.Value.Month).ThenBy(m => m.Birthdate!.Value.Day)
                .ToList();

            UpcomingAnniversaries = allMembers
                .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                .Where(m =>
                {
                    var a = m.Anniversary!.Value;
                    try { var t = new DateTime(today.Year, a.Month, a.Day); return t >= today && t <= in30days; }
                    catch { return false; }
                })
                .OrderBy(m => m.Anniversary!.Value.Month).ThenBy(m => m.Anniversary!.Value.Day)
                .ToList();
        }

        //  Search helpers 
        public bool FamilyMatchesSearch(Family f)
        {
            if (string.IsNullOrEmpty(Q)) return true;
            var q = Q.ToLower();
            return f.FamilyName.ToLower().Contains(q) ||
                   f.Members.Any(m => m.Name.ToLower().Contains(q) ||
                                      m.Surname.ToLower().Contains(q));
        }

        public bool MemberMatchesSearch(Member m)
        {
            if (string.IsNullOrEmpty(Q)) return true;
            var q = Q.ToLower();
            return m.Name.ToLower().Contains(q) || m.Surname.ToLower().Contains(q);
        }
    }
}

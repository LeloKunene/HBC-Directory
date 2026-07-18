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
        // Leadership — Elders and Deacons, shown in a collapsible section up top
        public List<Member> Leadership { get; set; } = new();

        // Staff assignments, shown in their own collapsible section up top
        public List<StaffAssignment> StaffAssignments { get; set; } = new();

        // All families (sorted alphabetically)
        public List<Family> Families { get; set; } = new();

        // Individual members — adults not in any family
        public List<Member> IndividualMembers { get; set; } = new();

        // MemberId -> staff role name(s), e.g. "Secretary". Staff are regular
        // members (shown alphabetically with everyone else, inside their
        // family if they have one) — this just lets their card/badge show
        // the role and lets search match on it.
        public Dictionary<int, List<string>> StaffRoles { get; set; } = new();

        public List<string> StaffRolesFor(int memberId) =>
            StaffRoles.TryGetValue(memberId, out var roles) ? roles : new List<string>();

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

            //  Staff role lookup (used to badge/search staff members within the unified list) 
            StaffAssignments = await _db.StaffAssignments
                .Include(sa => sa.Member).ThenInclude(m => m.Family)
                .Include(sa => sa.StaffRole)
                .OrderBy(sa => sa.DisplayOrder)
                .ToListAsync();
            StaffRoles = StaffAssignments
                .GroupBy(sa => sa.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(sa => sa.StaffRole.RoleName).ToList());

            //  Leadership 
            Leadership = await _db.Members
                .Include(m => m.Family)
                .Where(m => m.ChurchOffice == "Elder" || m.ChurchOffice == "Deacon")
                .OrderBy(m => m.ChurchOffice).ThenBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

            //  Families (alphabetical) 
            var familiesQ = _db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .AsQueryable();

            Families = await familiesQ.ToListAsync();

            //  Individual members (adults with no family) 
            // Note: search/status/office filtering now happens entirely client-side
            // (instant, no page reload) — see the script block at the bottom of Index.cshtml.
            // We always load the full set here so the client has everything to filter against.
            IndividualMembers = await _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

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

        //  Client-side search helpers 
        // Builds the lowercase blob of searchable text embedded in each family
        // card's data-search attribute, so the browser can filter instantly
        // without a server round trip.
        public string SearchTextFor(Family f)
        {
            var parts = new List<string> { f.FamilyName };
            foreach (var m in f.Members)
            {
                parts.Add(m.Name);
                parts.Add(m.Surname);
                if (!string.IsNullOrEmpty(m.ChurchOffice)) parts.Add(m.ChurchOffice);
                parts.AddRange(StaffRolesFor(m.Id));
            }
            return string.Join(" ", parts).ToLowerInvariant();
        }

        public string SearchTextFor(Member m)
        {
            var parts = new List<string> { m.Name, m.Surname };
            if (!string.IsNullOrEmpty(m.ChurchOffice)) parts.Add(m.ChurchOffice);
            parts.AddRange(StaffRolesFor(m.Id));
            return string.Join(" ", parts).ToLowerInvariant();
        }

        // Comma-separated list of distinct MemberStatus values present in the family
        // (e.g. "Member,Attendant"), used so the status filter can hide a family
        // that has nobody matching the selected status.
        public string StatusesFor(Family f) =>
            string.Join(",", f.Members
                .Where(m => !string.IsNullOrEmpty(m.MemberStatus))
                .Select(m => m.MemberStatus)
                .Distinct());

        // Same idea for ChurchOffice ("Elder"/"Deacon").
        public string OfficesFor(Family f) =>
            string.Join(",", f.Members
                .Where(m => !string.IsNullOrEmpty(m.ChurchOffice))
                .Select(m => m.ChurchOffice)
                .Distinct());
    }
}

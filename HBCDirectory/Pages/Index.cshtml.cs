using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly PhotoService _photos;

        public IndexModel(DirectoryContext db, PhotoService photos)
        {
            _db = db; _photos = photos;
        }

        // ── Directory sections ─────────────────────────────────────────────
        public List<Member>          Leadership       { get; set; } = new();
        public List<StaffAssignment> StaffAssignments { get; set; } = new();
        public List<Family>          Families         { get; set; } = new();
        public List<Member>          IndividualMembers{ get; set; } = new();

        public Dictionary<int, List<string>> StaffRoleLookup { get; set; } = new();

        // MemberId → group names e.g. ["Iron Men", "Growth Groups"]
        public Dictionary<int, List<string>> GroupLookup { get; set; } = new();
        public List<string> AllAssignedStaffRoles { get; set; } = new();
        public List<Group> AllGroups { get; set; } = new();
        public string? Q           { get; set; }
        public string? StatusFilter { get; set; }
        public string? OfficeFilter { get; set; }
        public string? StaffRoleFilter { get; set; }
        public string? GroupFilter     { get; set; }
        public string? CardTypeFilter  { get; set; }
        public List<Member> UpcomingBirthdays    { get; set; } = new();
        public List<Member> UpcomingAnniversaries{ get; set; } = new();

        public string PhotoUrl(string? f) => _photos.Url(f);

        public async Task OnGetAsync(
            string? q, string? status, string? office,
            string? staffrole, string? group, string? cardtype)
        {
            Q              = q?.Trim();
            StatusFilter   = status;
            OfficeFilter   = office;
            StaffRoleFilter = staffrole;
            GroupFilter    = group;
            CardTypeFilter = cardtype;

            // Staff assignments & role lookup
            StaffAssignments = await _db.StaffAssignments
                .Include(sa => sa.Member).ThenInclude(m => m.Family)
                .Include(sa => sa.StaffRole)
                .OrderBy(sa => sa.DisplayOrder)
                .ToListAsync();

            StaffRoleLookup = StaffAssignments
                .GroupBy(sa => sa.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(sa => sa.StaffRole.RoleName).ToList());

            AllAssignedStaffRoles = StaffAssignments
                .Select(sa => sa.StaffRole.RoleName)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            // Group lookup
            AllGroups = await _db.Groups.OrderBy(g => g.DisplayOrder).ToListAsync();

            var allMemberGroups = await _db.MemberGroups
                .Include(mg => mg.Group)
                .ToListAsync();

            GroupLookup = allMemberGroups
                .GroupBy(mg => mg.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(mg => mg.Group.Name).ToList());

            // Leadership
            Leadership = await _db.Members
                .Include(m => m.Family)
                .Where(m => m.ChurchOffice == "Elder" || m.ChurchOffice == "Deacon")
                .OrderBy(m => m.ChurchOffice).ThenBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

            // Families
            Families = await _db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .ToListAsync();

            // Individual members
            IndividualMembers = await _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

            // Notifications
            var today    = DateTime.Today;
            var in30days = today.AddDays(30);
            var everyone = await _db.Members.ToListAsync();

            UpcomingBirthdays = everyone
                .Where(m => m.Birthdate.HasValue && m.ShowBirthdate)
                .Where(m => { var d = m.Birthdate!.Value;
                    try { var t = new DateTime(today.Year, d.Month, d.Day); return t >= today && t <= in30days; }
                    catch { return false; } })
                .OrderBy(m => m.Birthdate!.Value.Month).ThenBy(m => m.Birthdate!.Value.Day)
                .ToList();

            UpcomingAnniversaries = everyone
                .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                .Where(m => { var a = m.Anniversary!.Value;
                    try { var t = new DateTime(today.Year, a.Month, a.Day); return t >= today && t <= in30days; }
                    catch { return false; } })
                .OrderBy(m => m.Anniversary!.Value.Month).ThenBy(m => m.Anniversary!.Value.Day)
                .ToList();
        }

        public List<string> StaffRolesFor(int memberId) =>
            StaffRoleLookup.TryGetValue(memberId, out var r) ? r : new();

        public List<string> GroupsFor(int memberId) =>
            GroupLookup.TryGetValue(memberId, out var g) ? g : new();

        // Union of all adults' groups in a family
        public List<string> GroupsFor(Family f) =>
            f.Adults
             .SelectMany(a => GroupsFor(a.Id))
             .Distinct()
             .ToList();

        // Union of all adults' staff roles in a family
        public List<string> StaffRolesFor(Family f) =>
            f.Adults
             .SelectMany(a => StaffRolesFor(a.Id))
             .Distinct()
             .ToList();

        // Searchable text blob embedded in data-search on each card
        public string SearchTextFor(Family f)
        {
            var parts = new List<string> { f.FamilyName };
            foreach (var m in f.Members)
            {
                parts.Add(m.Name); parts.Add(m.Surname);
                if (!string.IsNullOrEmpty(m.ChurchOffice)) parts.Add(m.ChurchOffice);
                parts.AddRange(StaffRolesFor(m.Id));
                parts.AddRange(GroupsFor(m.Id));
            }
            return string.Join(" ", parts).ToLowerInvariant();
        }

        public string SearchTextFor(Member m)
        {
            var parts = new List<string> { m.Name, m.Surname };
            if (!string.IsNullOrEmpty(m.MemberStatus))  parts.Add(m.MemberStatus);
            if (!string.IsNullOrEmpty(m.ChurchOffice))  parts.Add(m.ChurchOffice);
            parts.AddRange(StaffRolesFor(m.Id));
            parts.AddRange(GroupsFor(m.Id));
            return string.Join(" ", parts).ToLowerInvariant();
        }

        // Comma lists for family card filter attributes
        public string StatusesFor(Family f) =>
            string.Join(",", f.Members
                .Where(m => !string.IsNullOrEmpty(m.MemberStatus))
                .Select(m => m.MemberStatus).Distinct());

        public string OfficesFor(Family f) =>
            string.Join(",", f.Members
                .Where(m => !string.IsNullOrEmpty(m.ChurchOffice))
                .Select(m => m.ChurchOffice).Distinct());
    }
}

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
        private readonly IDbContextFactory<DirectoryContext> _dbFactory;

        public IndexModel(DirectoryContext db, PhotoService photos, IDbContextFactory<DirectoryContext> dbFactory)
        {
            _db = db; _photos = photos; _dbFactory = dbFactory;
        }

        // ── Directory sections ─────────────────────────────────────────────
        public List<Member>          Leadership       { get; set; } = new();
        public List<StaffAssignment> StaffAssignments { get; set; } = new();
        public List<Family>          Families         { get; set; } = new();
        public List<Member>          IndividualMembers{ get; set; } = new();

        public Dictionary<int, List<string>> StaffRoleLookup { get; set; } = new();

        // MemberId → group names e.g. ["Care Grop A", "Drivers"]
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

            /* These six reads don't depend on each other, so run them
                concurrently instead of one after another. Each uses its own
                short-lived DbContext from _dbFactory — a single DbContext
                can't safely run more than one query at a time.*/
            var staffAssignmentsTask  = LoadStaffAssignmentsAsync();
            var groupsTask            = LoadGroupsAsync();
            var memberGroupsTask      = LoadMemberGroupsAsync();
            var leadershipTask        = LoadLeadershipAsync();
            var familiesTask          = LoadFamiliesAsync();
            var individualMembersTask = LoadIndividualMembersAsync();

            await Task.WhenAll(
                staffAssignmentsTask, groupsTask, memberGroupsTask,
                leadershipTask, familiesTask, individualMembersTask);

            StaffAssignments     = await staffAssignmentsTask;
            AllGroups            = await groupsTask;
            var allMemberGroups  = await memberGroupsTask;
            Leadership           = await leadershipTask;
            Families             = await familiesTask;
            IndividualMembers    = await individualMembersTask;

            StaffRoleLookup = StaffAssignments
                .GroupBy(sa => sa.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(sa => sa.StaffRole.RoleName).ToList());

            AllAssignedStaffRoles = StaffAssignments
                .Select(sa => sa.StaffRole.RoleName)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            GroupLookup = allMemberGroups
                .GroupBy(mg => mg.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(mg => mg.Group.Name).ToList());

            /* Notifications
                The month/day-crossing-year-boundary check below still has to run
                in memory (EF Core can't translate "does this recurring date fall
                in the next 30 days" into SQL), but there's no reason to pull
                members who have no birthdate/anniversary, or who've hidden it,
                out of the database in the first place so filter those at the
                query level instead of loading the whole table.*/
            var today    = DateTime.Today;
            var in30days = today.AddDays(30);

            var birthdayCandidates = await _db.Members
                .Where(m => m.Birthdate.HasValue && m.ShowBirthdate)
                .ToListAsync();

            var anniversaryCandidates = await _db.Members
                .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                .ToListAsync();

            UpcomingBirthdays = birthdayCandidates
                .Where(m => { var d = m.Birthdate!.Value;
                    try { var t = new DateTime(today.Year, d.Month, d.Day); return t >= today && t <= in30days; }
                    catch { return false; } })
                .OrderBy(m => m.Birthdate!.Value.Month).ThenBy(m => m.Birthdate!.Value.Day)
                .ToList();

            UpcomingAnniversaries = anniversaryCandidates
                .Where(m => { var a = m.Anniversary!.Value;
                    try { var t = new DateTime(today.Year, a.Month, a.Day); return t >= today && t <= in30days; }
                    catch { return false; } })
                .OrderBy(m => m.Anniversary!.Value.Month).ThenBy(m => m.Anniversary!.Value.Day)
                .ToList();
        }

        /* Parallel loaders for OnGetAsync ──────────────────────────────────
            Each opens its own DbContext via _dbFactory so these can run
            concurrently with Task.WhenAll above. Read-only — nothing here
            calls SaveChanges, so entities not being tracked by the page's
            main _db instance doesn't matter.*/
        private async Task<List<StaffAssignment>> LoadStaffAssignmentsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.StaffAssignments
                .Include(sa => sa.Member).ThenInclude(m => m.Family)
                .Include(sa => sa.StaffRole)
                .OrderBy(sa => sa.DisplayOrder)
                .ToListAsync();
        }

        private async Task<List<Group>> LoadGroupsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Groups.OrderBy(g => g.DisplayOrder).ToListAsync();
        }

        private async Task<List<MemberGroup>> LoadMemberGroupsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.MemberGroups.Include(mg => mg.Group).ToListAsync();
        }

        private async Task<List<Member>> LoadLeadershipAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Members
                .Include(m => m.Family)
                .Where(m => m.ChurchOffice == "Elder" || m.ChurchOffice == "Deacon")
                .OrderBy(m => m.ChurchOffice == "Elder" ? 0 : 1)
                .ThenBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();
        }

        private async Task<List<Family>> LoadFamiliesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .ToListAsync();
        }

        private async Task<List<Member>> LoadIndividualMembersAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();
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

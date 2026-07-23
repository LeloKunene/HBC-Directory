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
        public Dictionary<int, List<string>> GroupLookup { get; set; } = new();
        public Dictionary<int, string> CareGroupLookup { get; set; } = new();
        // care group.
        public Dictionary<int, List<string>> CareGroupLeaderLookup { get; set; } = new();
        public List<string> AllAssignedStaffRoles { get; set; } = new();
        public List<Group> AllGroups { get; set; } = new();
        public List<CareGroup> AllCareGroups { get; set; } = new();
        public string? Q           { get; set; }
        public string? StatusFilter { get; set; }
        public string? OfficeFilter { get; set; }
        public string? StaffRoleFilter { get; set; }
        public string? GroupFilter     { get; set; }
        public string? CareGroupFilter { get; set; }
        public string? CardTypeFilter  { get; set; }
        public List<Member> UpcomingBirthdays    { get; set; } = new();
        public List<AnniversaryDisplayItem> UpcomingAnniversaries { get; set; } = new();
        public List<UpcomingItem> UpcomingCombined { get; set; } = new();

        public class UpcomingItem
        {
            public DateTime Date { get; set; }
            public string DateLabel { get; set; } = "";
            public string Kind { get; set; } = "birthday"; // "birthday" | "anniversary"
            public string TargetType { get; set; } = "member"; // "member" | "family"
            public int TargetId { get; set; }
            public int? FamilyIdFallback { get; set; }
            public List<string> PhotoUrls { get; set; } = new(); // 0, 1, or 2 (overlap)
            public string InitialsFallback { get; set; } = "";
        }

        public class AnniversaryDisplayItem
        {
            public string Names { get; set; } = "";
            public DateTime Date { get; set; }
            public int? Years { get; set; }
            public int? FamilyId { get; set; }
            public string TargetType { get; set; } = "member"; // "member" | "family"
            public int TargetId { get; set; }
            public List<string> PhotoUrls { get; set; } = new(); // family photo, or up to 2 spouse photos
            public string InitialsFallback { get; set; } = "";
        }

        public string PhotoUrl(string? f) => _photos.Url(f);

        public async Task OnGetAsync(
            string? q, string? status, string? office,
            string? staffrole, string? group, string? caregroup, string? cardtype)
        {
            Q              = q?.Trim();
            StatusFilter   = status;
            OfficeFilter   = office;
            StaffRoleFilter = staffrole;
            GroupFilter    = group;
            CareGroupFilter = caregroup;
            CardTypeFilter = cardtype;

            /* These six reads don't depend on each other, so run them
                concurrently instead of one after another. Each uses its own
                short-lived DbContext from _dbFactory — a single DbContext
                can't safely run more than one query at a time.*/
            var staffAssignmentsTask  = LoadStaffAssignmentsAsync();
            var groupsTask            = LoadGroupsAsync();
            var memberGroupsTask      = LoadMemberGroupsAsync();
            var careGroupMembersTask  = LoadCareGroupMembersAsync();
            var careGroupLeadersTask  = LoadCareGroupLeadersAsync();
            var leadershipTask        = LoadLeadershipAsync();
            var familiesTask          = LoadFamiliesAsync();
            var individualMembersTask = LoadIndividualMembersAsync();

            await Task.WhenAll(
                staffAssignmentsTask, groupsTask, memberGroupsTask, careGroupMembersTask, careGroupLeadersTask,
                leadershipTask, familiesTask, individualMembersTask);

            StaffAssignments     = await staffAssignmentsTask;
            AllGroups            = await groupsTask;
            AllCareGroups        = await _db.CareGroups.OrderBy(cg => cg.Name).ToListAsync();
            var allMemberGroups  = await memberGroupsTask;
            var allCareGroupMembers = await careGroupMembersTask;
            var allCareGroupLeaders = await careGroupLeadersTask;
            Leadership           = await leadershipTask;
            Families             = (await familiesTask).Where(f => f.Adults.Any() || f.Children.Any()).ToList();
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

            // Exclusive — one care group per member (enforced at the DB
            // level), so this is Id → single name, not Id → list.
            CareGroupLookup = allCareGroupMembers
                .ToDictionary(cgm => cgm.MemberId, cgm => cgm.CareGroup.Name);

            // Not exclusive — one person can lead more than one care group.
            CareGroupLeaderLookup = allCareGroupLeaders
                .GroupBy(cgl => cgl.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(cgl => cgl.CareGroup.Name).ToList());

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
                .Where(m => m.MemberStatus != "Resigned" && m.MemberStatus != "Excommunicated")
                .ToListAsync();

            var anniversaryCandidates = await _db.Members
                .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                .Where(m => m.MemberStatus != "Resigned" && m.MemberStatus != "Excommunicated")
                .ToListAsync();

            UpcomingBirthdays = birthdayCandidates
                .Select(m => (Member: m, Next: NextOccurrence(m.Birthdate!.Value, today, in30days)))
                .Where(x => x.Next.HasValue)
                .OrderBy(x => x.Next!.Value) // chronological — soonest first, correctly across a year boundary
                .Select(x => x.Member)
                .ToList();

            var upcomingAnniversaryMembers = anniversaryCandidates
                .Select(m => (Member: m, Next: NextOccurrence(m.Anniversary!.Value, today, in30days)))
                .Where(x => x.Next.HasValue)
                .ToList();

            UpcomingAnniversaries = upcomingAnniversaryMembers
                .GroupBy(x => x.Member.FamilyId.HasValue
                    ? $"fam{x.Member.FamilyId}-{x.Member.Anniversary!.Value:yyyyMMdd}"
                    : $"solo{x.Member.Id}")
                .Select(g =>
                {
                    var pair = g.OrderBy(x => x.Member.Name).ToList();
                    var weddingDate = pair[0].Member.Anniversary!.Value;
                    var next        = pair[0].Next!.Value; // this year's or next year's occurrence, whichever is upcoming
                    var years       = next.Year - weddingDate.Year;

                    string names = pair.Count == 2 && pair[0].Member.Surname == pair[1].Member.Surname
                        ? $"{pair[0].Member.Name} & {pair[1].Member.Name} {pair[0].Member.Surname}"
                        : string.Join(" & ", pair.Select(x => x.Member.DisplayName));

                    var familyId = pair[0].Member.FamilyId;
                    var family = familyId.HasValue ? Families.FirstOrDefault(f => f.Id == familyId.Value) : null;

                    List<string> photoUrls;
                    if (family != null && !string.IsNullOrEmpty(family.PhotoFileName))
                        photoUrls = new List<string> { PhotoUrl(family.PhotoFileName) };
                    else
                        photoUrls = pair
                            .Where(x => !string.IsNullOrEmpty(x.Member.PhotoFileName))
                            .Take(2)
                            .Select(x => PhotoUrl(x.Member.PhotoFileName))
                            .ToList();

                    return new AnniversaryDisplayItem
                    {
                        Names = names,
                        Date  = next,
                        Years = years >= 0 ? years : null, // guards against a bad/placeholder year on file
                        FamilyId = familyId,
                        TargetType = family != null ? "family" : "member",
                        TargetId = family != null ? family.Id : pair[0].Member.Id,
                        PhotoUrls = photoUrls,
                        InitialsFallback = string.Join("", pair.Take(2).Select(x => x.Member.Name.FirstOrDefault()))
                    };
                })
                .OrderBy(x => x.Date) // chronological: soonest first, correctly across a year boundary
                .ToList();

            // Single merged, sorted feed for the "Coming up this month" strip.
            UpcomingCombined = UpcomingBirthdays
                .Select(m =>
                {
                    var next = NextOccurrence(m.Birthdate!.Value, today, in30days)!.Value;
                    return new UpcomingItem
                    {
                        Date = next,
                        DateLabel = next.ToString("MMM d"),
                        Kind = "birthday",
                        TargetType = "member",
                        TargetId = m.Id,
                        FamilyIdFallback = m.FamilyId,
                        PhotoUrls = string.IsNullOrEmpty(m.PhotoFileName) ? new() : new List<string> { PhotoUrl(m.PhotoFileName) },
                        InitialsFallback = $"{m.Name.FirstOrDefault()}{m.Surname.FirstOrDefault()}"
                    };
                })
                .Concat(UpcomingAnniversaries.Select(a => new UpcomingItem
                {
                    Date = a.Date,
                    DateLabel = a.Date.ToString("MMM d"),
                    Kind = "anniversary",
                    TargetType = a.TargetType,
                    TargetId = a.TargetId,
                    FamilyIdFallback = a.FamilyId,
                    PhotoUrls = a.PhotoUrls,
                    InitialsFallback = a.InitialsFallback
                }))
                .OrderBy(x => x.Date)
                .ToList();
        }

        private static DateTime? NextOccurrence(DateTime recurring, DateTime today, DateTime windowEnd)
        {
            try
            {
                var thisYear = new DateTime(today.Year, recurring.Month, recurring.Day);
                if (thisYear >= today && thisYear <= windowEnd) return thisYear;

                var nextYear = new DateTime(today.Year + 1, recurring.Month, recurring.Day);
                if (nextYear >= today && nextYear <= windowEnd) return nextYear;

                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /* Parallel loaders for OnGetAsync
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
                .Where(sa => sa.Member.MemberStatus != "Resigned" && sa.Member.MemberStatus != "Excommunicated")
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

        private async Task<List<CareGroupMember>> LoadCareGroupMembersAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.CareGroupMembers.Include(cgm => cgm.CareGroup).ToListAsync();
        }

        private async Task<List<CareGroupLeader>> LoadCareGroupLeadersAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.CareGroupLeaders.Include(cgl => cgl.CareGroup).ToListAsync();
        }

        private async Task<List<Member>> LoadLeadershipAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Members
                .Include(m => m.Family)
                .Where(m => m.ChurchOffice == "Elder" || m.ChurchOffice == "Deacon")
                .Where(m => m.MemberStatus != "Resigned" && m.MemberStatus != "Excommunicated")
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
                .Where(m => m.MemberStatus != "Resigned" && m.MemberStatus != "Excommunicated")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();
        }

        public List<string> StaffRolesFor(int memberId) =>
            StaffRoleLookup.TryGetValue(memberId, out var r) ? r : new();

        public List<string> GroupsFor(int memberId) =>
            GroupLookup.TryGetValue(memberId, out var g) ? g : new();

        public string? CareGroupFor(int memberId)
        {
            var parts = new List<string>();
            var isLeaderOf = CareGroupLeaderLookup.TryGetValue(memberId, out var led) ? led : null;
            if (isLeaderOf != null)
                parts.AddRange(isLeaderOf.Select(name => $"{name} (Leader)"));

            if (CareGroupLookup.TryGetValue(memberId, out var memberOf) &&
                (isLeaderOf == null || !isLeaderOf.Contains(memberOf)))
                parts.Add(memberOf);

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        public List<string> CareGroupNamesFor(int memberId)
        {
            var names = new List<string>();
            if (CareGroupLeaderLookup.TryGetValue(memberId, out var led)) names.AddRange(led);
            if (CareGroupLookup.TryGetValue(memberId, out var memberOf) && !names.Contains(memberOf))
                names.Add(memberOf);
            return names;
        }

        // Union of all adults' groups in a family
        public List<string> GroupsFor(Family f) =>
            f.Adults
             .SelectMany(a => GroupsFor(a.Id))
             .Distinct()
             .ToList();

        public List<string> CareGroupsFor(Family f) =>
            f.Adults
             .SelectMany(a => CareGroupNamesFor(a.Id))
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
                var careGroup = CareGroupFor(m.Id);
                if (!string.IsNullOrEmpty(careGroup)) parts.Add(careGroup);
            }
            return string.Join(" ", parts).ToLowerInvariant();
        }

        public string SearchTextFor(Member m)
        {
            var parts = new List<string> { m.Name, m.Surname };
            var publicStatus = Member.PublicStatus(m.MemberStatus);
            if (!string.IsNullOrEmpty(publicStatus))    parts.Add(publicStatus);
            if (!string.IsNullOrEmpty(m.ChurchOffice))  parts.Add(m.ChurchOffice);
            parts.AddRange(StaffRolesFor(m.Id));
            parts.AddRange(GroupsFor(m.Id));
            var careGroup = CareGroupFor(m.Id);
            if (!string.IsNullOrEmpty(careGroup)) parts.Add(careGroup);
            return string.Join(" ", parts).ToLowerInvariant();
        }

        // Comma lists for family card filter attributes
        public string StatusesFor(Family f) =>
            string.Join(",", f.Members
                .Select(m => Member.PublicStatus(m.MemberStatus))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct());

        public string OfficesFor(Family f) =>
            string.Join(",", f.Members
                .Where(m => !string.IsNullOrEmpty(m.ChurchOffice))
                .Select(m => m.ChurchOffice).Distinct());

        public string? PublicStatusFor(Member m) => Member.PublicStatus(m.MemberStatus);
    }
}

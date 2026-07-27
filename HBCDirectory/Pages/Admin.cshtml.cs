using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using iText.Kernel.Pdf;
using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Threading.RateLimiting;

namespace HBCDirectory.Pages
{
    [Authorize(Roles = "Admin,Leadership")]
    public class AdminModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly IDbContextFactory<DirectoryContext> _dbFactory;
        private readonly IConfiguration   _config;
        private readonly PhotoService      _photos;
        private readonly TokenService      _tokens;
        private readonly EmailService      _email;
        private readonly DirectoryPdfService _pdfService;
        private readonly RateLimiter        _pdfGenerateLimiter;

        public AdminModel(DirectoryContext db, IDbContextFactory<DirectoryContext> dbFactory,
                          IConfiguration config, PhotoService photos,
                          TokenService tokens, EmailService email, DirectoryPdfService pdfService,
                          [FromKeyedServices("pdfgenerate")] RateLimiter pdfGenerateLimiter)
        {
            _db = db; _dbFactory = dbFactory; _config = config; _photos = photos; _tokens = tokens; _email = email; _pdfService = pdfService;
            _pdfGenerateLimiter = pdfGenerateLimiter;
        }

        public List<Member>          Members          { get; set; } = new();
        public List<Family>          Families         { get; set; } = new();
        public List<StaffRole>       StaffRoles       { get; set; } = new();
        public List<StaffAssignment> StaffAssignments { get; set; } = new();
        public List<Group>         Groups         { get; set; } = new();
        public List<MemberGroup>   MemberGroups   { get; set; } = new();
        public List<CareGroup>     CareGroups     { get; set; } = new();
        public List<PendingUpdate> PendingUpdates { get; set; } = new();
        public List<PendingFamilyPhoto> PendingFamilyPhotos { get; set; } = new();
        public List<ChangeLog>     RecentChanges  { get; set; } = new();
        public List<(string Label, int Value)> Stats { get; set; } = new();
        public ApprovalSettings ApprovalConfig { get; set; } = new();
        public PdfSettings PdfConfig { get; set; } = new();

        public string PhotoUrl(string? f) => _photos.Url(f);

        public string? AdminDisplayStatus(Member m) =>
            m.MemberStatus is "Pending Removal" or "Pending Discipline" ? "Member" : m.MemberStatus;

        public IEnumerable<Member> VisibleAdultsFor(Family f) =>
            f.Members.Where(m => m.MemberType == "Adult" && Member.IsVisibleToCongregation(m))
                     .OrderBy(m => m.Surname).ThenBy(m => m.Name);

        public IEnumerable<Member> VisibleMembersFor(Family f) =>
            f.Members.Where(Member.IsVisibleToCongregation);

        public string FamilyDisambiguated(Family f)
        {
            var sameNameCount = Families.Count(x => x.FamilyName == f.FamilyName);
            if (sameNameCount <= 1) return f.FamilyName;
            return f.HeadOfFamily != null
                ? $"{f.FamilyName} ({f.HeadOfFamily.Name})"
                : $"{f.FamilyName} — no head set";
        }

        public async Task OnGetAsync()
        {
            var membersTask            = LoadMembersAsync();
            var familiesTask           = LoadFamiliesAsync();
            var staffRolesTask         = LoadStaffRolesAsync();
            var staffAssignmentsTask   = LoadStaffAssignmentsAsync();
            var groupsTask             = LoadGroupsAsync();
            var memberGroupsTask       = LoadMemberGroupsAsync();
            var careGroupsTask         = LoadCareGroupsAsync();
            var pendingUpdatesTask     = LoadPendingUpdatesAsync();
            var pendingFamilyPhotosTask = LoadPendingFamilyPhotosAsync();
            var recentChangesTask      = LoadRecentChangesAsync();
            var pdfConfigTask          = LoadPdfConfigAsync();
            var approvalConfigTask     = LoadApprovalConfigAsync();

            await Task.WhenAll(
                membersTask, familiesTask, staffRolesTask, staffAssignmentsTask,
                groupsTask, memberGroupsTask, careGroupsTask, pendingUpdatesTask,
                pendingFamilyPhotosTask, recentChangesTask, pdfConfigTask, approvalConfigTask);

            Members             = await membersTask;
            Families            = await familiesTask;
            StaffRoles          = await staffRolesTask;
            StaffAssignments    = await staffAssignmentsTask;
            Groups              = await groupsTask;
            MemberGroups        = await memberGroupsTask;
            CareGroups          = await careGroupsTask;
            PendingUpdates      = await pendingUpdatesTask;
            PendingFamilyPhotos = await pendingFamilyPhotosTask;
            RecentChanges       = await recentChangesTask;
            PdfConfig           = await pdfConfigTask;
            ApprovalConfig      = await approvalConfigTask;

            var adults   = Members.Count(m => m.MemberType == "Adult");
            var children = Members.Count(m => m.MemberType == "Child");
            var leaders  = Members.Count(m => m.ChurchOffice is "Elder" or "Deacon");

            Stats = new List<(string, int)>
            {
                ("Members",   Members.Count),
                ("Families",  Families.Count),
                ("Adults",    adults),
                ("Children",  children),
            };
            if (leaders > 0)            Stats.Add(("Leadership", leaders));
            if (StaffAssignments.Any()) Stats.Add(("Staff", StaffAssignments.Count));
        }

        private async Task<List<Member>> LoadMembersAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Members.Include(m => m.Family)
                .Where(m => m.MemberStatus != "Resigned" && m.MemberStatus != "Excommunicated")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();
        }

        private async Task<List<Family>> LoadFamiliesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Families.Include(f => f.Members).Include(f => f.HeadOfFamily)
                .OrderBy(f => f.FamilyName).ToListAsync();
        }

        private async Task<List<StaffRole>> LoadStaffRolesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.StaffRoles.OrderBy(sr => sr.DisplayOrder).ToListAsync();
        }

        private async Task<List<StaffAssignment>> LoadStaffAssignmentsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.StaffAssignments
                .Include(sa => sa.Member).Include(sa => sa.StaffRole)
                .OrderBy(sa => sa.DisplayOrder).ToListAsync();
        }

        private async Task<List<Group>> LoadGroupsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Groups.OrderBy(g => g.DisplayOrder).ToListAsync();
        }

        private async Task<List<MemberGroup>> LoadMemberGroupsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.MemberGroups.Include(mg => mg.Member).Include(mg => mg.Group).ToListAsync();
        }

        private async Task<List<CareGroup>> LoadCareGroupsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.CareGroups
                .Include(cg => cg.Leaders).ThenInclude(l => l.Member)
                .Include(cg => cg.Members).ThenInclude(m => m.Member)
                .OrderBy(cg => cg.Name).ToListAsync();
        }

        private async Task<List<PendingUpdate>> LoadPendingUpdatesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.PendingUpdates.Include(p => p.Member)
                .Where(p => !p.IsApproved && !p.IsRejected)
                .OrderBy(p => p.SubmittedAt).ToListAsync();
        }

        private async Task<List<PendingFamilyPhoto>> LoadPendingFamilyPhotosAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.PendingFamilyPhotos.Include(p => p.Family)
                .Where(p => !p.IsApproved && !p.IsRejected)
                .OrderBy(p => p.SubmittedAt).ToListAsync();
        }

        private async Task<List<ChangeLog>> LoadRecentChangesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.ChangeLogs
                .Where(c => c.Action != "Status changed")
                .OrderByDescending(c => c.ChangedAt).Take(30).ToListAsync();
        }

        private async Task<PdfSettings> LoadPdfConfigAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.PdfSettings.FindAsync(1) ?? new PdfSettings();
        }

        private async Task<ApprovalSettings> LoadApprovalConfigAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.ApprovalSettings.FindAsync(1) ?? new ApprovalSettings();
        }

        //  Add Member 
        public async Task<IActionResult> OnPostAddMemberAsync(
            string name, string surname, string? email,
            string memberType, string? memberStatus, string? churchOffice,
            DateTime? birthdate, DateTime? anniversary, DateTime? dateJoined,
            string? phoneNumber, string? address, int? familyId, IFormFile? photo)
        {
            memberType = string.IsNullOrWhiteSpace(memberType) ? "Adult" : memberType;
            bool isAdult   = memberType == "Adult";
            bool isMember  = (memberStatus ?? "Member") == "Member";

            if (isAdult && isMember && string.IsNullOrWhiteSpace(email))
            { TempData["Error"] = "Email is required for members."; return Redirect("/Admin#section-members"); }
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            { TempData["Error"] = "Name and surname are required."; return Redirect("/Admin#section-members"); }

            if (isAdult && !string.IsNullOrWhiteSpace(email))
            {
                var lower = email.Trim().ToLower();
                if (await _db.Members.AnyAsync(m => m.Email == lower))
                { TempData["Error"] = $"Email '{email}' already exists."; return Redirect("/Admin#section-members"); }
            }

            try
            {
                var member = new Member
                {
                    Name         = CapFirst(name),
                    Surname      = CapFirst(surname),
                    Email        = isAdult && !string.IsNullOrWhiteSpace(email) ? email.Trim().ToLower() : null,
                    MemberType   = memberType,
                    MemberStatus = isAdult ? (memberStatus ?? "Member") : null,
                    ChurchOffice = isAdult && memberStatus == "Member"
                                    ? (string.IsNullOrEmpty(churchOffice) ? null : churchOffice)
                                    : null,
                    Birthdate   = birthdate,
                    Anniversary = anniversary,
                    DateJoined  = dateJoined,
                    PhoneNumber = phoneNumber?.Trim(),
                    Address     = address?.Trim(),
                    FamilyId    = familyId
                };

                if (photo is { Length: > 0 })
                {
                    var err = ValidatePhoto(photo);
                    if (err != null) { TempData["Error"] = err; return Redirect("/Admin#section-members"); }
                    if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return Redirect("/Admin#section-members"); }
                    member.PhotoFileName = await SavePhotoAsync(photo);
                }

                _db.Members.Add(member);
                await _db.SaveChangesAsync();

                if (isAdult && isMember && !string.IsNullOrEmpty(member.Email))
                    await CreateMemberAccountAsync(member);

                TempData["Success"] = $"'{member.DisplayName}' added.";
                await LogChangeAsync("Member", member.Id, member.DisplayName, "Created");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); TempData["Error"] = "Could not add member."; }
            return Redirect("/Admin#section-members");
        }

        private async Task CreateMemberAccountAsync(Member member)
        {
            var tmp = GenerateTempPassword();
            _db.MemberAccounts.Add(new MemberAccount
            {
                MemberId     = member.Id,
                Username     = member.Email!,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tmp)
            });
            await _db.SaveChangesAsync();

            var token = await _tokens.CreateTokenAsync(member.Email!, TimeSpan.FromDays(3));
            var link  = $"{Request.Scheme}://{Request.Host}/ResetPassword?token={token}";
            await _email.SendWelcomeEmailAsync(member.Email!, member.DisplayName, tmp, link);
        }

        //  Edit Member 
        public async Task<IActionResult> OnPostEditMemberAsync(
            int id, string name, string surname, string? email,
            string memberType, string? memberStatus, string? churchOffice,
            DateTime? birthdate, DateTime? anniversary, DateTime? dateJoined,
            string? phoneNumber, string? address,
            int? familyId, IFormFile? photo,
            bool showPhone, bool showAddress, bool showBirthdate, bool showAnniversary)
        {
            var m = await _db.Members.FindAsync(id);
            if (m == null) return NotFound();

            bool isAdult  = memberType == "Adult";
            bool isMember = (memberStatus ?? "Member") == "Member";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            { TempData["Error"] = "Name and surname are required."; return Redirect("/Admin#section-members"); }

            var effectiveEmail = !string.IsNullOrWhiteSpace(email) ? email.Trim() : m.Email;
            if (isAdult && isMember && string.IsNullOrWhiteSpace(effectiveEmail))
            { TempData["Error"] = "Email is required for members."; return Redirect("/Admin#section-members"); }

            var hasLeadershipManagedStatus = m.MemberStatus is
                "Pending Removal" or "Pending Discipline" or "Resigned" or "Excommunicated";

            m.Name         = CapFirst(name);
            m.Surname      = CapFirst(surname);
            m.MemberType   = memberType;
            m.MemberStatus = isAdult
                ? (hasLeadershipManagedStatus ? m.MemberStatus : (memberStatus ?? "Member"))
                : null;
            m.ChurchOffice = isAdult && !hasLeadershipManagedStatus && memberStatus == "Member"
                              ? (string.IsNullOrEmpty(churchOffice) ? null : churchOffice)
                              : null;
            m.Birthdate    = birthdate;
            m.Anniversary  = anniversary;
            m.DateJoined   = dateJoined;
            m.PhoneNumber  = phoneNumber?.Trim();
            m.Address      = address?.Trim();

            var headOfFamily = await _db.Families.FirstOrDefaultAsync(fam => fam.HeadOfFamilyId == m.Id);
            if (headOfFamily != null && (headOfFamily.Id != familyId || !isAdult))
                headOfFamily.HeadOfFamilyId = null;

            m.FamilyId     = familyId;
            m.ShowPhone    = showPhone;
            m.ShowAddress  = showAddress;
            m.ShowBirthdate    = showBirthdate;
            m.ShowAnniversary  = showAnniversary;

            if (isAdult && !string.IsNullOrWhiteSpace(email))
            {
                var newEmail = email.Trim().ToLower();
                if (newEmail != m.Email)
                {
                    if (await _db.Members.AnyAsync(x => x.Email == newEmail && x.Id != id))
                    { TempData["Error"] = $"Email '{email}' is already in use."; return Redirect("/Admin#section-members"); }
                    m.Email = newEmail;
                    var acct = await _db.MemberAccounts.FirstOrDefaultAsync(a => a.MemberId == id);
                    if (acct != null) acct.Username = newEmail;
                }
            }

            if (photo is { Length: > 0 })
            {
                if (!string.IsNullOrEmpty(m.PhotoFileName)) await DeleteFromR2Async(m.PhotoFileName);
                var err = ValidatePhoto(photo);
                if (err != null) { TempData["Error"] = err; return Redirect("/Admin#section-members"); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return Redirect("/Admin#section-members"); }
                m.PhotoFileName = await SavePhotoAsync(photo);
            }

            await _db.SaveChangesAsync();


            if (isAdult && isMember && !string.IsNullOrEmpty(m.Email))
            {
                var hasAccount = await _db.MemberAccounts.AnyAsync(a => a.MemberId == id);
                if (!hasAccount)
                    await CreateMemberAccountAsync(m);
            }

            await LogChangeAsync("Member", m.Id, m.DisplayName, "Updated");
            TempData["Success"] = $"'{m.DisplayName}' updated.";
            return Redirect("/Admin#section-members");
        }

        //  Delete Member 
        public async Task<IActionResult> OnPostDeleteMemberAsync(int id)
        {
            var m = await _db.Members.FindAsync(id);
            if (m == null) return NotFound();
            var memberName = m.DisplayName;
            if (!string.IsNullOrEmpty(m.PhotoFileName)) await DeleteFromR2Async(m.PhotoFileName);
            _db.Members.Remove(m);
            await _db.SaveChangesAsync();
            await LogChangeAsync("Member", id, memberName, "Deleted");
            TempData["Success"] = "Member deleted.";
            return Redirect("/Admin#section-members");
        }

        //  Add Family 
        public async Task<IActionResult> OnPostAddFamilyAsync(
            string familyName, string? address, string? familyPhone,
            string? additionalNotes, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            { TempData["Error"] = "Family name is required."; return Redirect("/Admin#section-families"); }

            var f = new Family
            {
                FamilyName      = CapFirst(familyName),
                Address         = address?.Trim(),
                FamilyPhone     = familyPhone?.Trim(),
                AdditionalNotes = additionalNotes?.Trim()
            };

            if (photo is { Length: > 0 })
            {
                var err = ValidatePhoto(photo);
                if (err != null) { TempData["Error"] = err; return Redirect("/Admin#section-families"); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return Redirect("/Admin#section-families"); }
                f.PhotoFileName = await SavePhotoAsync(photo);
            }

            _db.Families.Add(f);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Family '{f.FamilyName}' created.";
            return Redirect("/Admin#section-families");
        }

        //  Edit Family 
        public async Task<IActionResult> OnPostEditFamilyAsync(
            int id, string familyName, string? address, string? familyPhone,
            string? additionalNotes, int? headOfFamilyId, IFormFile? photo)
        {
            var f = await _db.Families.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();
            if (string.IsNullOrWhiteSpace(familyName))
            { TempData["Error"] = "Family name is required."; return Redirect("/Admin#section-families"); }
            f.HeadOfFamilyId = headOfFamilyId.HasValue &&
                f.Members.Any(m => m.Id == headOfFamilyId.Value && m.MemberType == "Adult")
                    ? headOfFamilyId
                    : null;

            f.FamilyName      = CapFirst(familyName);
            f.Address         = address?.Trim();
            f.FamilyPhone     = familyPhone?.Trim();
            f.AdditionalNotes = additionalNotes?.Trim();

            if (photo is { Length: > 0 })
            {
                if (!string.IsNullOrEmpty(f.PhotoFileName)) await DeleteFromR2Async(f.PhotoFileName);
                var err = ValidatePhoto(photo);
                if (err != null) { TempData["Error"] = err; return Redirect("/Admin#section-families"); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return Redirect("/Admin#section-families"); }
                f.PhotoFileName = await SavePhotoAsync(photo);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Family '{f.FamilyName}' updated.";
            return Redirect("/Admin#section-families");
        }

        //  Delete Family 
        public async Task<IActionResult> OnPostDeleteFamilyAsync(int id)
        {
            // Adults → individual members (unlink)
            var adults = await _db.Members
                .Where(m => m.FamilyId == id && m.MemberType == "Adult").ToListAsync();
            foreach (var a in adults) a.FamilyId = null;
            await _db.SaveChangesAsync();

            // Children → deleted via cascade
            var family = await _db.Families.Include(f => f.Members)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (family == null) return NotFound();
            if (!string.IsNullOrEmpty(family.PhotoFileName))
                await DeleteFromR2Async(family.PhotoFileName);

            _db.Families.Remove(family);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Family deleted. Adults moved to Individual Members.";
            return Redirect("/Admin#section-families");
        }

        //  Staff Role Management 
        public async Task<IActionResult> OnPostAddStaffRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            { TempData["Error"] = "Role name is required."; return Redirect("/Admin#section-staff"); }
            var maxOrd = await _db.StaffRoles.AnyAsync()
                ? await _db.StaffRoles.MaxAsync(sr => sr.DisplayOrder) : 0;
            _db.StaffRoles.Add(new StaffRole { RoleName = roleName.Trim(), DisplayOrder = maxOrd + 1 });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Staff role '{roleName}' created.";
            return Redirect("/Admin#section-staff");
        }

        public async Task<IActionResult> OnPostDeleteStaffRoleAsync(int id)
        {
            var role = await _db.StaffRoles.FindAsync(id);
            if (role == null) return NotFound();
            _db.StaffRoles.Remove(role);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Staff role deleted.";
            return Redirect("/Admin#section-staff");
        }

        public async Task<IActionResult> OnPostAssignStaffAsync(
            int memberId, int staffRoleId, string? bio, int displayOrder)
        {
            var alreadyHasThisRole = await _db.StaffAssignments
                .AnyAsync(sa => sa.MemberId == memberId && sa.StaffRoleId == staffRoleId);
            if (alreadyHasThisRole)
            {
                TempData["Error"] = "This member already has that staff role.";
                return Redirect("/Admin#section-staff");
            }

            _db.StaffAssignments.Add(new StaffAssignment
            {
                MemberId = memberId, StaffRoleId = staffRoleId,
                Bio = bio?.Trim(), DisplayOrder = displayOrder
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Staff assignment saved.";
            return Redirect("/Admin#section-staff");
        }

        public async Task<IActionResult> OnPostRemoveStaffAsync(int id)
        {
            var sa = await _db.StaffAssignments.FindAsync(id);
            if (sa == null) return NotFound();
            _db.StaffAssignments.Remove(sa);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Staff assignment removed.";
            return Redirect("/Admin#section-staff");
        }

        // Children: no email, no login account, no MemberStatus, no ChurchOffice.
        public async Task<IActionResult> OnPostAddChildAsync(
            int familyId, string name, string surname,
            DateTime? birthdate, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            { TempData["Error"] = "Name and surname are required."; return Redirect("/Admin#section-families"); }

            var family = await _db.Families.FindAsync(familyId);
            if (family == null) return NotFound();

            var child = new Member
            {
                Name         = CapFirst(name),
                Surname      = CapFirst(surname),
                Email        = null,
                MemberType   = "Child",
                MemberStatus = null,
                ChurchOffice = null,
                Birthdate    = birthdate,
                FamilyId     = familyId
            };

            if (photo is { Length: > 0 })
            {
                var err = ValidatePhoto(photo);
                if (err != null) { TempData["Error"] = err; return Redirect("/Admin#section-families"); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return Redirect("/Admin#section-families"); }
                child.PhotoFileName = await SavePhotoAsync(photo);
            }

            _db.Members.Add(child);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"\'{child.DisplayName}\' added to {family.FamilyName} family.";
            return Redirect("/Admin#section-families");
        }

        // To remove an adult from a family, use Edit Member and clear the family field.
        public async Task<IActionResult> OnPostRemoveChildAsync(int id)
        {
            var child = await _db.Members.FindAsync(id);
            if (child == null) return NotFound();

            if (child.MemberType != "Child")
            {
                TempData["Error"] = "To remove an adult from a family use Edit Member.";
                return Redirect("/Admin#section-families");
            }

            if (!string.IsNullOrEmpty(child.PhotoFileName))
                await DeleteFromR2Async(child.PhotoFileName);

            _db.Members.Remove(child);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"\'{child.DisplayName}\' removed.";
            return Redirect("/Admin#section-families");
        }

        public async Task<IActionResult> OnPostAddGroupAsync(string groupName, string? description)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            { TempData["Error"] = "Group name is required."; return Redirect("/Admin#section-groups"); }

            var maxOrd = await _db.Groups.AnyAsync()
                ? await _db.Groups.MaxAsync(g => g.DisplayOrder) : 0;

            _db.Groups.Add(new Group
            {
                Name = groupName.Trim(),
                Description = description?.Trim(),
                DisplayOrder = maxOrd + 1
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Group '{groupName}' created.";
            return Redirect("/Admin#section-groups");
        }

        public async Task<IActionResult> OnPostEditGroupAsync(int id, string groupName, string? description)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            { TempData["Error"] = "Group name is required."; return Redirect("/Admin#section-groups"); }

            var group = await _db.Groups.FindAsync(id);
            if (group == null) return NotFound();

            group.Name = groupName.Trim();
            group.Description = description?.Trim();
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Group renamed to '{group.Name}'.";
            return Redirect("/Admin#section-groups");
        }

        public async Task<IActionResult> OnPostDeleteGroupAsync(int id)
        {
            var group = await _db.Groups.FindAsync(id);
            if (group == null) return NotFound();
            _db.Groups.Remove(group);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Group deleted.";
            return Redirect("/Admin#section-groups");
        }

        public async Task<IActionResult> OnPostAddMemberToGroupAsync(int memberId, int groupId)
        {
            var exists = await _db.MemberGroups
                .AnyAsync(mg => mg.MemberId == memberId && mg.GroupId == groupId);
            if (!exists)
            {
                _db.MemberGroups.Add(new MemberGroup { MemberId = memberId, GroupId = groupId });
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Member added to group.";
            return Redirect("/Admin#section-groups");
        }

        public async Task<IActionResult> OnPostRemoveMemberFromGroupAsync(int id)
        {
            var mg = await _db.MemberGroups.FindAsync(id);
            if (mg == null) return NotFound();
            _db.MemberGroups.Remove(mg);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Member removed from group.";
            return Redirect("/Admin#section-groups");
        }

        //  Care Groups (pastoral care — standalone from Groups & Ministries) 
        public async Task<IActionResult> OnPostAddCareGroupAsync(string careGroupName)
        {
            if (string.IsNullOrWhiteSpace(careGroupName))
            { TempData["Error"] = "Care group name is required."; return Redirect("/Admin#section-caregroups"); }

            _db.CareGroups.Add(new CareGroup { Name = careGroupName.Trim() });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Care group '{careGroupName}' created.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostEditCareGroupAsync(int id, string careGroupName)
        {
            if (string.IsNullOrWhiteSpace(careGroupName))
            { TempData["Error"] = "Care group name is required."; return Redirect("/Admin#section-caregroups"); }

            var cg = await _db.CareGroups.FindAsync(id);
            if (cg == null) return NotFound();

            cg.Name = careGroupName.Trim();
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Care group renamed to '{cg.Name}'.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostDeleteCareGroupAsync(int id)
        {
            var cg = await _db.CareGroups.FindAsync(id);
            if (cg == null) return NotFound();
            _db.CareGroups.Remove(cg);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Care group deleted.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostAddCareGroupLeaderAsync(int memberId, int careGroupId)
        {
            var exists = await _db.CareGroupLeaders
                .AnyAsync(l => l.MemberId == memberId && l.CareGroupId == careGroupId);
            if (!exists)
            {
                _db.CareGroupLeaders.Add(new CareGroupLeader { MemberId = memberId, CareGroupId = careGroupId });
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Leader added to care group.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostRemoveCareGroupLeaderAsync(int id)
        {
            var leader = await _db.CareGroupLeaders.FindAsync(id);
            if (leader == null) return NotFound();
            _db.CareGroupLeaders.Remove(leader);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Leader removed from care group.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostAddMemberToCareGroupAsync(int memberId, int careGroupId)
        {
            var existing = await _db.CareGroupMembers.FirstOrDefaultAsync(m => m.MemberId == memberId);
            if (existing != null)
            {
                if (existing.CareGroupId == careGroupId)
                { TempData["Error"] = "That member is already in this care group."; return Redirect("/Admin#section-caregroups"); }
                existing.CareGroupId = careGroupId;
            }
            else
            {
                _db.CareGroupMembers.Add(new CareGroupMember { MemberId = memberId, CareGroupId = careGroupId });
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = "Member added to care group.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostRemoveMemberFromCareGroupAsync(int id)
        {
            var cgm = await _db.CareGroupMembers.FindAsync(id);
            if (cgm == null) return NotFound();
            _db.CareGroupMembers.Remove(cgm);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Member removed from care group.";
            return Redirect("/Admin#section-caregroups");
        }

        public async Task<IActionResult> OnPostApprovePendingAsync(int id)
        {
            var pending = await _db.PendingUpdates
                .Include(p => p.Member)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pending == null) return NotFound();

            var member = pending.Member;
            var oldName = member.DisplayName;

            try
            {
                var changes = System.Text.Json.JsonDocument.Parse(pending.ChangesJson).RootElement;
                if (changes.TryGetProperty("name",          out var n)) member.Name          = n.GetString()!;
                if (changes.TryGetProperty("surname",        out var s)) member.Surname       = s.GetString()!;
                if (changes.TryGetProperty("phoneNumber",    out var p)) member.PhoneNumber   = p.GetString();
                if (changes.TryGetProperty("address",        out var addr)) member.Address    = addr.GetString();
                if (changes.TryGetProperty("showPhone",      out var sp)) member.ShowPhone     = sp.GetBoolean();
                if (changes.TryGetProperty("showBirthdate",  out var sb)) member.ShowBirthdate = sb.GetBoolean();
                if (changes.TryGetProperty("showAnniversary",out var sa)) member.ShowAnniversary = sa.GetBoolean();
                if (changes.TryGetProperty("birthdate",   out var bd))
                    member.Birthdate   = bd.ValueKind == System.Text.Json.JsonValueKind.Null ? null : bd.GetDateTime();
                if (changes.TryGetProperty("anniversary", out var av))
                    member.Anniversary = av.ValueKind == System.Text.Json.JsonValueKind.Null ? null : av.GetDateTime();
                if (changes.TryGetProperty("dateJoined",  out var dj))
                    member.DateJoined  = dj.ValueKind == System.Text.Json.JsonValueKind.Null ? null : dj.GetDateTime();
            }
            catch (Exception ex) { Console.WriteLine($"PendingUpdate parse error: {ex.Message}"); }

            // Apply pending photo if any
            if (!string.IsNullOrEmpty(pending.PendingPhotoFileName))
            {
                if (!string.IsNullOrEmpty(member.PhotoFileName))
                    await DeleteFromR2Async(member.PhotoFileName);
                member.PhotoFileName = pending.PendingPhotoFileName;
            }

            pending.IsApproved = true;
            pending.ReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await LogChangeAsync("Member", member.Id, member.DisplayName, "Updated",
                $"Profile update approved (was: {oldName})");

            TempData["Success"] = $"Update for '{member.DisplayName}' approved.";
            return Redirect("/Admin#section-pendingupdates");
        }

        public async Task<IActionResult> OnPostRejectPendingAsync(int id, string? reviewNote)
        {
            var pending = await _db.PendingUpdates
                .Include(p => p.Member)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pending == null) return NotFound();

            // Delete the pending photo from R2 if it was uploaded
            if (!string.IsNullOrEmpty(pending.PendingPhotoFileName))
                await DeleteFromR2Async(pending.PendingPhotoFileName);

            pending.IsRejected = true;
            pending.ReviewedAt = DateTime.UtcNow;
            pending.ReviewNote = reviewNote?.Trim();
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Update for '{pending.Member.DisplayName}' rejected.";
            return Redirect("/Admin#section-pendingupdates");
        }

        public async Task<IActionResult> OnPostApprovePendingFamilyPhotoAsync(int id)
        {
            var pending = await _db.PendingFamilyPhotos
                .Include(p => p.Family)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pending == null) return NotFound();

            var family = pending.Family;

            if (!string.IsNullOrEmpty(family.PhotoFileName))
                await DeleteFromR2Async(family.PhotoFileName);
            family.PhotoFileName = pending.PendingPhotoFileName;

            pending.IsApproved = true;
            pending.ReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await LogChangeAsync("Family", family.Id, family.FamilyName, "Updated", "Family photo approved");

            TempData["Success"] = $"Family photo for '{family.FamilyName}' approved.";
            return Redirect("/Admin#section-pendingphotos");
        }

        public async Task<IActionResult> OnPostRejectPendingFamilyPhotoAsync(int id, string? reviewNote)
        {
            var pending = await _db.PendingFamilyPhotos
                .Include(p => p.Family)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pending == null) return NotFound();

            await DeleteFromR2Async(pending.PendingPhotoFileName);

            pending.IsRejected = true;
            pending.ReviewedAt = DateTime.UtcNow;
            pending.ReviewNote = reviewNote?.Trim();
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Family photo submission for '{pending.Family.FamilyName}' rejected.";
            return Redirect("/Admin#section-pendingphotos");
        }


        public async Task<IActionResult> OnPostSaveApprovalSettingsAsync(
            bool requireName, bool requirePhone, bool requirePrivacy, bool requirePhoto,
            bool requireBirthdate, bool requireAnniversary, bool requireDateJoined)
        {
            var settings = await _db.ApprovalSettings.FindAsync(1);
            if (settings == null) { settings = new ApprovalSettings { Id = 1 }; _db.ApprovalSettings.Add(settings); }
            settings.RequireApprovalForName    = requireName;
            settings.RequireApprovalForPhone   = requirePhone;
            settings.RequireApprovalForPrivacy = requirePrivacy;
            settings.RequireApprovalForPhoto   = requirePhoto;
            settings.RequireApprovalForBirthdate   = requireBirthdate;
            settings.RequireApprovalForAnniversary = requireAnniversary;
            settings.RequireApprovalForDateJoined  = requireDateJoined;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Approval settings saved.";
            return Redirect("/Admin#section-approval");
        }

        public async Task<IActionResult> OnPostGeneratePdfAsync()
        {
            using var lease = await _pdfGenerateLimiter.AcquireAsync(1);
            if (!lease.IsAcquired)
            {
                TempData["Error"] = "PDF generation is limited to 10 per hour. Please try again later.";
                return Redirect("/Admin#section-pdf");
            }

            var settings = await _db.PdfSettings.FindAsync(1);
            if (settings == null)
            {
                settings = new PdfSettings { Id = 1 };
                _db.PdfSettings.Add(settings);
            }

            await RegenerateAndCachePdfAsync(settings);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"PDF generated and stored. Ready to download.";
            return Redirect("/Admin#section-pdf");
        }

        public async Task<IActionResult> OnPostSavePdfSettingsAsync(
            string pagesJson, string? password, bool removePassword)
        {
            var settings = await _db.PdfSettings.FindAsync(1);
            if (settings == null) { settings = new PdfSettings { Id = 1 }; _db.PdfSettings.Add(settings); }

            settings.PagesJson = pagesJson;
            if (removePassword)
                settings.Password = null;
            else if (!string.IsNullOrWhiteSpace(password))
                settings.Password = password.Trim();

            if (removePassword && settings.R2Key != null)
            {
                using var lease = await _pdfGenerateLimiter.AcquireAsync(1);
                if (!lease.IsAcquired)
                {
                    TempData["Error"] = "PDF generation is limited to 10 per hour. The password field was saved, but the stored PDF could not be regenerated yet — try 'Update PDF' shortly.";
                    await _db.SaveChangesAsync();
                    return Redirect("/Admin#section-pdf");
                }

                await RegenerateAndCachePdfAsync(settings);
                TempData["Success"] = "Password removed. The stored PDF has been updated.";
            }
            else
            {
                TempData["Success"] = "PDF settings saved. Click 'Update PDF' to apply changes.";
            }

            await _db.SaveChangesAsync();
            return Redirect("/Admin#section-pdf");
        }

        private async Task RegenerateAndCachePdfAsync(PdfSettings settings)
        {
            var families = await _db.Families
                .Include(f => f.Members)
                .AsNoTracking()
                .OrderBy(f => f.FamilyName)
                .ToListAsync();

            var unassigned = await _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .AsNoTracking()
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

            foreach (var f in families)
                f.Members = f.Members
                    .Where(Member.IsVisibleToCongregation)
                    .Select(SanitizedForPdf)
                    .ToList();
            unassigned = unassigned.Where(Member.IsVisibleToCongregation).Select(SanitizedForPdf).ToList();

            static Member SanitizedForPdf(Member m)
            {
                m.MemberStatus = Member.PublicStatus(m.MemberStatus);
                return m;
            }

            var pages = settings.GetPages();
            var bytes = await _pdfService.GenerateAsync(families, unassigned, pages);

            if (settings.HasPassword)
                bytes = PdfPasswordHelper.AddPassword(bytes, settings.Password!);
            var key = $"pdf/hbc-directory-{Guid.NewGuid():N}.pdf";

            var previousKey = settings.R2Key;

            await _pdfService.UploadToR2Async(bytes, key);

            settings.R2Key         = key;
            settings.LastGenerated = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(previousKey) && previousKey != key)
                await DeleteFromR2Async(previousKey);
        }

        //  Helpers 
        private static string CapFirst(string s)
        { s = s.Trim(); return s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..]; }

        private static string? ValidatePhoto(IFormFile p)
        {
            var ok = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(p.FileName).ToLowerInvariant();
            if (!ok.Contains(ext)) return "Photo must be JPG, PNG, or WEBP.";
            if (p.Length > 5 * 1024 * 1024) return "Photo must be under 5 MB.";
            return null;
        }

        private static async Task<bool> IsImageAsync(IFormFile p)
        {
            var buf = new byte[4];
            using var s = p.OpenReadStream();
            _ = await s.ReadAsync(buf.AsMemory(0, 4));
            return (buf[0] == 0xFF && buf[1] == 0xD8) ||
                   (buf[0] == 0x89 && buf[1] == 0x50) ||
                   (buf[0] == 0x52 && buf[1] == 0x49);
        }

        private async Task<string> SavePhotoAsync(IFormFile p)
        {
            var fn  = Guid.NewGuid() + Path.GetExtension(p.FileName).ToLowerInvariant();
            var cred = new BasicAWSCredentials(_config["R2:AccessKeyId"], _config["R2:SecretAccessKey"]);
            var cfg  = new AmazonS3Config { ServiceURL = _config["R2:Endpoint"], ForcePathStyle = true };
            using var client = new AmazonS3Client(cred, cfg);
            using var stream = p.OpenReadStream();
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _config["R2:BucketName"], Key = fn,
                InputStream = stream, ContentType = p.ContentType, DisablePayloadSigning = true
            });
            return fn;
        }

        private async Task DeleteFromR2Async(string fn)
        {
            try
            {
                var cred = new BasicAWSCredentials(_config["R2:AccessKeyId"], _config["R2:SecretAccessKey"]);
                var cfg  = new AmazonS3Config { ServiceURL = _config["R2:Endpoint"], ForcePathStyle = true };
                using var client = new AmazonS3Client(cred, cfg);
                await client.DeleteObjectAsync(_config["R2:BucketName"], fn);
                Console.WriteLine($"Deleted from R2: {fn}");
            }
            catch (Exception ex) { Console.WriteLine($"R2 delete failed: {ex.Message}"); }
        }

        private static string GenerateTempPassword()
        {
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            var rnd = new byte[12];
            RandomNumberGenerator.Fill(rnd);
            return new string(rnd.Select(b => chars[b % chars.Length]).ToArray());
        }

        private async Task LogChangeAsync(
            string entityType, int entityId, string entityName, string action, string? notes = null)
        {
            _db.ChangeLogs.Add(new ChangeLog
            {
                ChangedAt  = DateTime.UtcNow,
                ChangedBy  = User.Identity?.Name ?? "admin",
                EntityType = entityType,
                EntityId   = entityId,
                EntityName = entityName,
                Action     = action,
                Notes      = notes
            });
            await _db.SaveChangesAsync();
        }

        public static class PdfPasswordHelper
        {
            public static byte[] AddPassword(byte[] pdfBytes, string password)
            {
                using var input  = new MemoryStream(pdfBytes);
                using var output = new MemoryStream();
                var reader = new PdfReader(input);

                var ownerPassword = RandomNumberGenerator.GetBytes(32);

                var writerProps = new WriterProperties()
                    .SetStandardEncryption(
                        System.Text.Encoding.UTF8.GetBytes(password),
                        ownerPassword,
                        EncryptionConstants.ALLOW_PRINTING | EncryptionConstants.ALLOW_COPY,
                        EncryptionConstants.ENCRYPTION_AES_128
                    );
                var writer = new PdfWriter(output, writerProps);
                var doc    = new PdfDocument(reader, writer);
                doc.Close();
                return output.ToArray();
            }
        }
    }
}

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
    [Authorize(Roles = "Admin")]
    public class AdminModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly IConfiguration   _config;
        private readonly PhotoService      _photos;
        private readonly TokenService      _tokens;
        private readonly EmailService      _email;
        private readonly DirectoryPdfService _pdfService;
        private readonly RateLimiter        _pdfGenerateLimiter;

        public AdminModel(DirectoryContext db, IConfiguration config, PhotoService photos,
                          TokenService tokens, EmailService email, DirectoryPdfService pdfService,
                          [FromKeyedServices("pdfgenerate")] RateLimiter pdfGenerateLimiter)
        {
            _db = db; _config = config; _photos = photos; _tokens = tokens; _email = email; _pdfService = pdfService;
            _pdfGenerateLimiter = pdfGenerateLimiter;
        }

        public List<Member>          Members          { get; set; } = new();
        public List<Family>          Families         { get; set; } = new();
        public List<StaffRole>       StaffRoles       { get; set; } = new();
        public List<StaffAssignment> StaffAssignments { get; set; } = new();
        public List<Group>         Groups         { get; set; } = new();
        public List<MemberGroup>   MemberGroups   { get; set; } = new();
        public List<PendingUpdate> PendingUpdates { get; set; } = new();
        public List<ChangeLog>     RecentChanges  { get; set; } = new();
        public List<(string Label, int Value)> Stats { get; set; } = new();
        public ApprovalSettings ApprovalConfig { get; set; } = new();
        public PdfSettings PdfConfig { get; set; } = new();

        public string PhotoUrl(string? f) => _photos.Url(f);

        public async Task OnGetAsync()
        {
            Members = await _db.Members.Include(m => m.Family)
                .OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();
            Families = await _db.Families.Include(f => f.Members)
                .OrderBy(f => f.FamilyName).ToListAsync();
            StaffRoles = await _db.StaffRoles.OrderBy(sr => sr.DisplayOrder).ToListAsync();
            StaffAssignments = await _db.StaffAssignments
                .Include(sa => sa.Member).Include(sa => sa.StaffRole)
                .OrderBy(sa => sa.DisplayOrder).ToListAsync();
            Groups = await _db.Groups.OrderBy(g => g.DisplayOrder).ToListAsync();
            MemberGroups = await _db.MemberGroups
                .Include(mg => mg.Member).Include(mg => mg.Group).ToListAsync();
            PendingUpdates = await _db.PendingUpdates.Include(p => p.Member)
                .Where(p => !p.IsApproved && !p.IsRejected)
                .OrderBy(p => p.SubmittedAt).ToListAsync();
            RecentChanges = await _db.ChangeLogs
                .OrderByDescending(c => c.ChangedAt).Take(30).ToListAsync();
            PdfConfig = await _db.PdfSettings.FindAsync(1) ?? new PdfSettings();

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

            ApprovalConfig   = await _db.ApprovalSettings.FindAsync(1) ?? new ApprovalSettings();
            Groups           = await _db.Groups.OrderBy(g => g.DisplayOrder).ToListAsync();
            MemberGroups     = await _db.MemberGroups.Include(mg => mg.Member).Include(mg => mg.Group).ToListAsync();
            PendingUpdates   = await _db.PendingUpdates.Include(p => p.Member)
                                   .Where(p => !p.IsApproved && !p.IsRejected)
                                   .OrderBy(p => p.SubmittedAt).ToListAsync();
            RecentChanges    = await _db.ChangeLogs.OrderByDescending(c => c.ChangedAt).Take(30).ToListAsync();
            if (StaffAssignments.Any()) Stats.Add(("Staff", StaffAssignments.Count));
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

            /*  Only Members get a login
                Attendants shouldn't have directory access until they become a
                Member, so there's no reason to force an email address on them.*/
            if (isAdult && isMember && string.IsNullOrWhiteSpace(email))
            { TempData["Error"] = "Email is required for members."; return RedirectToPage(); }
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            { TempData["Error"] = "Name and surname are required."; return RedirectToPage(); }

            if (isAdult && !string.IsNullOrWhiteSpace(email))
            {
                var lower = email.Trim().ToLower();
                if (await _db.Members.AnyAsync(m => m.Email == lower))
                { TempData["Error"] = $"Email '{email}' already exists."; return RedirectToPage(); }
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
                    if (err != null) { TempData["Error"] = err; return RedirectToPage(); }
                    if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return RedirectToPage(); }
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
            return RedirectToPage();
        }

        /*  Grants directory login access: creates a MemberAccount with a
            temporary password and emails the member a reset link. Only ever
            called for Members with an email set. Attendants shouldn't have
            access until they're promoted to Member*/
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

            var token = await _tokens.CreateTokenAsync(member.Email!, TimeSpan.FromHours(24));
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
            { TempData["Error"] = "Name and surname are required."; return RedirectToPage(); }

            var effectiveEmail = !string.IsNullOrWhiteSpace(email) ? email.Trim() : m.Email;
            if (isAdult && isMember && string.IsNullOrWhiteSpace(effectiveEmail))
            { TempData["Error"] = "Email is required for members."; return RedirectToPage(); }

            m.Name         = CapFirst(name);
            m.Surname      = CapFirst(surname);
            m.MemberType   = memberType;
            m.MemberStatus = isAdult ? (memberStatus ?? "Member") : null;
            m.ChurchOffice = isAdult && memberStatus == "Member"
                              ? (string.IsNullOrEmpty(churchOffice) ? null : churchOffice)
                              : null;
            m.Birthdate    = birthdate;
            m.Anniversary  = anniversary;
            m.DateJoined   = dateJoined;
            m.PhoneNumber  = phoneNumber?.Trim();
            m.Address      = address?.Trim();
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
                    { TempData["Error"] = $"Email '{email}' is already in use."; return RedirectToPage(); }
                    m.Email = newEmail;
                    var acct = await _db.MemberAccounts.FirstOrDefaultAsync(a => a.MemberId == id);
                    if (acct != null) acct.Username = newEmail;
                }
            }

            if (photo is { Length: > 0 })
            {
                if (!string.IsNullOrEmpty(m.PhotoFileName)) await DeleteFromR2Async(m.PhotoFileName);
                var err = ValidatePhoto(photo);
                if (err != null) { TempData["Error"] = err; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return RedirectToPage(); }
                m.PhotoFileName = await SavePhotoAsync(photo);
            }

            await _db.SaveChangesAsync();

            /*  Promoted from Attendant to Member (or was already a Member but
                never had an account, e.g. was added before an email existed)
                and now has an email —> grant directory access.*/
            if (isAdult && isMember && !string.IsNullOrEmpty(m.Email))
            {
                var hasAccount = await _db.MemberAccounts.AnyAsync(a => a.MemberId == id);
                if (!hasAccount)
                    await CreateMemberAccountAsync(m);
            }

            await LogChangeAsync("Member", m.Id, m.DisplayName, "Updated");
            TempData["Success"] = $"'{m.DisplayName}' updated.";
            return RedirectToPage();
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
            return RedirectToPage();
        }

        //  Add Family 
        public async Task<IActionResult> OnPostAddFamilyAsync(
            string familyName, string? address, string? familyPhone,
            string? additionalNotes, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            { TempData["Error"] = "Family name is required."; return RedirectToPage(); }

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
                if (err != null) { TempData["Error"] = err; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return RedirectToPage(); }
                f.PhotoFileName = await SavePhotoAsync(photo);
            }

            _db.Families.Add(f);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Family '{f.FamilyName}' created.";
            return RedirectToPage();
        }

        //  Edit Family 
        public async Task<IActionResult> OnPostEditFamilyAsync(
            int id, string familyName, string? address, string? familyPhone,
            string? additionalNotes, IFormFile? photo)
        {
            var f = await _db.Families.FindAsync(id);
            if (f == null) return NotFound();
            if (string.IsNullOrWhiteSpace(familyName))
            { TempData["Error"] = "Family name is required."; return RedirectToPage(); }

            f.FamilyName      = CapFirst(familyName);
            f.Address         = address?.Trim();
            f.FamilyPhone     = familyPhone?.Trim();
            f.AdditionalNotes = additionalNotes?.Trim();

            if (photo is { Length: > 0 })
            {
                if (!string.IsNullOrEmpty(f.PhotoFileName)) await DeleteFromR2Async(f.PhotoFileName);
                var err = ValidatePhoto(photo);
                if (err != null) { TempData["Error"] = err; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return RedirectToPage(); }
                f.PhotoFileName = await SavePhotoAsync(photo);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Family '{f.FamilyName}' updated.";
            return RedirectToPage();
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
            return RedirectToPage();
        }

        //  Staff Role Management 
        public async Task<IActionResult> OnPostAddStaffRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            { TempData["Error"] = "Role name is required."; return RedirectToPage(); }
            var maxOrd = await _db.StaffRoles.AnyAsync()
                ? await _db.StaffRoles.MaxAsync(sr => sr.DisplayOrder) : 0;
            _db.StaffRoles.Add(new StaffRole { RoleName = roleName.Trim(), DisplayOrder = maxOrd + 1 });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Staff role '{roleName}' created.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteStaffRoleAsync(int id)
        {
            var role = await _db.StaffRoles.FindAsync(id);
            if (role == null) return NotFound();
            _db.StaffRoles.Remove(role);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Staff role deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAssignStaffAsync(
            int memberId, int staffRoleId, string? bio, int displayOrder)
        {
            var existing = await _db.StaffAssignments.FirstOrDefaultAsync(sa => sa.MemberId == memberId);
            if (existing != null) _db.StaffAssignments.Remove(existing);
            _db.StaffAssignments.Add(new StaffAssignment
            {
                MemberId = memberId, StaffRoleId = staffRoleId,
                Bio = bio?.Trim(), DisplayOrder = displayOrder
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Staff assignment saved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveStaffAsync(int id)
        {
            var sa = await _db.StaffAssignments.FindAsync(id);
            if (sa == null) return NotFound();
            _db.StaffAssignments.Remove(sa);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Staff assignment removed.";
            return RedirectToPage();
        }

        // Children: no email, no login account, no MemberStatus, no ChurchOffice.
        public async Task<IActionResult> OnPostAddChildAsync(
            int familyId, string name, string surname,
            DateTime? birthdate, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            { TempData["Error"] = "Name and surname are required."; return RedirectToPage(); }

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
                if (err != null) { TempData["Error"] = err; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Invalid image."; return RedirectToPage(); }
                child.PhotoFileName = await SavePhotoAsync(photo);
            }

            _db.Members.Add(child);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"\'{child.DisplayName}\' added to {family.FamilyName} family.";
            return RedirectToPage();
        }

        // To remove an adult from a family, use Edit Member and clear the family field.
        public async Task<IActionResult> OnPostRemoveChildAsync(int id)
        {
            var child = await _db.Members.FindAsync(id);
            if (child == null) return NotFound();

            if (child.MemberType != "Child")
            {
                TempData["Error"] = "To remove an adult from a family use Edit Member.";
                return RedirectToPage();
            }

            if (!string.IsNullOrEmpty(child.PhotoFileName))
                await DeleteFromR2Async(child.PhotoFileName);

            _db.Members.Remove(child);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"\'{child.DisplayName}\' removed.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddGroupAsync(string groupName, string? description)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            { TempData["Error"] = "Group name is required."; return RedirectToPage(); }

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
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteGroupAsync(int id)
        {
            var group = await _db.Groups.FindAsync(id);
            if (group == null) return NotFound();
            _db.Groups.Remove(group);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Group deleted.";
            return RedirectToPage();
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
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveMemberFromGroupAsync(int id)
        {
            var mg = await _db.MemberGroups.FindAsync(id);
            if (mg == null) return NotFound();
            _db.MemberGroups.Remove(mg);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Member removed from group.";
            return RedirectToPage();
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
                if (changes.TryGetProperty("showPhone",      out var sp)) member.ShowPhone     = sp.GetBoolean();
                if (changes.TryGetProperty("showBirthdate",  out var sb)) member.ShowBirthdate = sb.GetBoolean();
                if (changes.TryGetProperty("showAnniversary",out var sa)) member.ShowAnniversary = sa.GetBoolean();
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
            return RedirectToPage();
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
            return RedirectToPage();
        }


        public async Task<IActionResult> OnPostSaveApprovalSettingsAsync(
            bool requireName, bool requirePhone, bool requirePrivacy, bool requirePhoto)
        {
            var settings = await _db.ApprovalSettings.FindAsync(1);
            if (settings == null) { settings = new ApprovalSettings { Id = 1 }; _db.ApprovalSettings.Add(settings); }
            settings.RequireApprovalForName    = requireName;
            settings.RequireApprovalForPhone   = requirePhone;
            settings.RequireApprovalForPrivacy = requirePrivacy;
            settings.RequireApprovalForPhoto   = requirePhoto;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Approval settings saved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostGeneratePdfAsync()
        {
            using var lease = await _pdfGenerateLimiter.AcquireAsync(1);
            if (!lease.IsAcquired)
            {
                TempData["Error"] = "PDF generation is limited to 10 per hour. Please try again later.";
                return RedirectToPage();
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
            return RedirectToPage();
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

            // If a password was just removed and a PDF is already cached in R2,
            // that cached file was encrypted at generation time — the setting
            // alone can't strip it, so regenerate it now to match. This does
            // the same R2 write + QuestPDF render as "Update PDF", so it draws
            // from the same rate limit.
            if (removePassword && settings.R2Key != null)
            {
                using var lease = await _pdfGenerateLimiter.AcquireAsync(1);
                if (!lease.IsAcquired)
                {
                    TempData["Error"] = "PDF generation is limited to 10 per hour. The password field was saved, but the stored PDF could not be regenerated yet — try 'Update PDF' shortly.";
                    await _db.SaveChangesAsync();
                    return RedirectToPage();
                }

                await RegenerateAndCachePdfAsync(settings);
                TempData["Success"] = "Password removed. The stored PDF has been updated.";
            }
            else
            {
                TempData["Success"] = "PDF settings saved. Click 'Update PDF' to apply changes.";
            }

            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        private async Task RegenerateAndCachePdfAsync(PdfSettings settings)
        {
            var families = await _db.Families
                .Include(f => f.Members)
                .OrderBy(f => f.FamilyName)
                .ToListAsync();

            var unassigned = await _db.Members
                .Where(m => m.FamilyId == null && m.MemberType == "Adult")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();

            var pages = settings.GetPages();
            var bytes = await _pdfService.GenerateAsync(families, unassigned, pages);

            if (settings.HasPassword)
                bytes = PdfPasswordHelper.AddPassword(bytes, settings.Password!);

            /* A predictable, date-based key would let anyone who guesses the
                pattern download the directory PDF directly from R2 if the
                bucket were ever accidentally made public — use a random key
                instead so it's only reachable via the authenticated Download
                page, which looks it up from settings.R2Key in the database.*/
            var key = $"pdf/hbc-directory-{Guid.NewGuid():N}.pdf";

            /* Each generation used to write a brand-new key without removing
                the previous one, so old PDFs (containing member photos, phone
                numbers, addresses) accumulated in R2 forever. Keep track of the
                old key and remove it once the new upload has succeeded.*/
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

                /* The owner password grants full control over the PDF (removing
                    the user password, changing permissions, etc). Deriving it from
                    the user password ("<password>_o") meant anyone who knew the
                    open password could trivially guess it. Thus we now use an independent
                    random value each time so nobody needs to remember it,
                    it only needs to exist so the encryption has an owner.*/
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

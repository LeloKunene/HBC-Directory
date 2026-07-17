using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

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

        public AdminModel(DirectoryContext db, IConfiguration config, PhotoService photos,
                          TokenService tokens, EmailService email)
        {
            _db = db; _config = config; _photos = photos; _tokens = tokens; _email = email;
        }

        public List<Member>          Members          { get; set; } = new();
        public List<Family>          Families         { get; set; } = new();
        public List<StaffRole>       StaffRoles       { get; set; } = new();
        public List<StaffAssignment> StaffAssignments { get; set; } = new();
        public int TotalMembers  { get; set; }
        public int TotalFamilies { get; set; }
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
            TotalMembers  = Members.Count;
            TotalFamilies = Families.Count;
        }

        // ── Add Member ────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddMemberAsync(
            string name, string surname, string? email,
            string memberType, string? memberStatus, string? churchOffice,
            DateTime? birthdate, DateTime? anniversary,
            string? phoneNumber, int? familyId, IFormFile? photo)
        {
            memberType = string.IsNullOrWhiteSpace(memberType) ? "Adult" : memberType;
            bool isAdult = memberType == "Adult";

            if (isAdult && string.IsNullOrWhiteSpace(email))
            { TempData["Error"] = "Email is required for adults."; return RedirectToPage(); }
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
                    Email        = isAdult ? email!.Trim().ToLower() : null,
                    MemberType   = memberType,
                    MemberStatus = isAdult ? (memberStatus ?? "Member") : null,
                    ChurchOffice = isAdult && memberStatus == "Member"
                                    ? (string.IsNullOrEmpty(churchOffice) ? null : churchOffice)
                                    : null,
                    Birthdate   = birthdate,
                    Anniversary = anniversary,
                    PhoneNumber = phoneNumber?.Trim(),
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

                if (isAdult && !string.IsNullOrEmpty(member.Email))
                {
                    var tmp  = GenerateTempPassword();
                    _db.MemberAccounts.Add(new MemberAccount
                    {
                        MemberId     = member.Id,
                        Username     = member.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(tmp)
                    });
                    await _db.SaveChangesAsync();

                    var token = await _tokens.CreateTokenAsync(member.Email, TimeSpan.FromHours(24));
                    var link  = $"{Request.Scheme}://{Request.Host}/ResetPassword?token={token}";
                    await _email.SendWelcomeEmailAsync(member.Email, member.DisplayName, tmp, link);
                }

                TempData["Success"] = $"'{member.DisplayName}' added.";
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); TempData["Error"] = "Could not add member."; }
            return RedirectToPage();
        }

        // ── Edit Member ───────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostEditMemberAsync(
            int id, string name, string surname, string? email,
            string memberType, string? memberStatus, string? churchOffice,
            DateTime? birthdate, DateTime? anniversary, string? phoneNumber,
            int? familyId, IFormFile? photo,
            bool showPhone, bool showBirthdate, bool showAnniversary)
        {
            var m = await _db.Members.FindAsync(id);
            if (m == null) return NotFound();

            bool isAdult = memberType == "Adult";
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            { TempData["Error"] = "Name and surname are required."; return RedirectToPage(); }

            m.Name         = CapFirst(name);
            m.Surname      = CapFirst(surname);
            m.MemberType   = memberType;
            m.MemberStatus = isAdult ? (memberStatus ?? "Member") : null;
            m.ChurchOffice = isAdult && memberStatus == "Member"
                              ? (string.IsNullOrEmpty(churchOffice) ? null : churchOffice)
                              : null;
            m.Birthdate   = birthdate;
            m.Anniversary = anniversary;
            m.PhoneNumber = phoneNumber?.Trim();
            m.FamilyId    = familyId;
            m.ShowPhone        = showPhone;
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
            TempData["Success"] = $"'{m.DisplayName}' updated.";
            return RedirectToPage();
        }

        // ── Delete Member ─────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostDeleteMemberAsync(int id)
        {
            var m = await _db.Members.FindAsync(id);
            if (m == null) return NotFound();
            if (!string.IsNullOrEmpty(m.PhotoFileName)) await DeleteFromR2Async(m.PhotoFileName);
            _db.Members.Remove(m);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Member deleted.";
            return RedirectToPage();
        }

        // ── Add Family ────────────────────────────────────────────────────────
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

        // ── Edit Family ───────────────────────────────────────────────────────
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

        // ── Delete Family ─────────────────────────────────────────────────────
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

        // ── Staff Role Management ─────────────────────────────────────────────
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

        // ── Helpers ───────────────────────────────────────────────────────────
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
    }
}

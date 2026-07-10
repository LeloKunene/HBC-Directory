using HBCDirectory.Data;
using HBCDirectory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    [Authorize]
    public class AdminModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };
        private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

        public AdminModel(DirectoryContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<Family> Families { get; set; } = new();
        public List<Member> Members { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? EditMemberId { get; set; }
        public Member? EditingMember { get; set; }

        public async Task OnGetAsync()
        {
            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await _db.Members.Include(m => m.Family).OrderBy(m => m.Surname).ToListAsync();

            if (EditMemberId.HasValue)
                EditingMember = Members.FirstOrDefault(m => m.Id == EditMemberId.Value);
        }

        private string? ValidatePhoto(IFormFile photo)
        {
            if (photo.Length > MaxFileSizeBytes)
                return $"Photo must be smaller than 2MB. Your file is {photo.Length / 1024 / 1024}MB.";

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return $"Only JPG and PNG files are allowed. Got: {ext}";

            return null; // null means valid
        }

        /** Checks the actual bytes at the start of the file to confirm it is a real image.
        This is called "magic bytes" checking. It protects against someone renaming
        malware.exe to malware.jpg to bypass the extension check above.*/
        private static async Task<bool> IsImageAsync(IFormFile file)
        {
            var header = new byte[4];
            using var stream = file.OpenReadStream();
            await stream.ReadAsync(header, 0, 4);
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
            return false;
        }

        /** Saves an uploaded photo to wwwroot/uploads and returns the generated filename.
            We use Guid.NewGuid() to generate a random filename so two members uploading
            the same file don't overwrite each other.*/
        private async Task<string> SavePhotoAsync(IFormFile photo)
        {
            var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName).ToLowerInvariant();
            var filePath = Path.Combine(uploads, fileName);
            using var fs = System.IO.File.Create(filePath);
            await photo.CopyToAsync(fs);
            return fileName;
            Members = await _db.Members.Include(m => m.Family).OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostAddFamilyAsync(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            {
                TempData["Error"] = "Family name cannot be empty.";
                return RedirectToPage();
            }
            _db.Families.Add(new Family { FamilyName = familyName.Trim() });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Family '{familyName.Trim()}' added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditFamilyAsync(int familyId, string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            {
                TempData["Error"] = "Family name cannot be empty.";
                return RedirectToPage();
            }

            var family = await _db.Families.FindAsync(familyId);
            if (family == null) return NotFound();

            var oldName = family.FamilyName;
            family.FamilyName = familyName.Trim();
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Family renamed from '{oldName}' to '{family.FamilyName}'.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteFamilyAsync(int familyId)
        {
            var f = await _db.Families.FindAsync(familyId);
            if (f != null)
            {
                _db.Families.Remove(f);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Family '{f.FamilyName}' deleted.";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddMemberAsync(
            string name, string surname, DateTime? birthdate,
            DateTime? anniversary, string? phoneNumber, int? familyId, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            {
                TempData["Error"] = "Name and surname are required.";
                return RedirectToPage();
            }

            var member = new Member
            {
                Name = name.Trim(),
                Surname = surname.Trim(),
                Birthdate = birthdate,
                Anniversary = anniversary,
                PhoneNumber = phoneNumber?.Trim(),
                FamilyId = familyId
            };

            if (photo != null && photo.Length > 0)
            {
                var error = ValidatePhoto(photo);
                if (error != null) { TempData["Error"] = error; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Not a valid image."; return RedirectToPage(); }
                member.PhotoFileName = await SavePhotoAsync(photo);
            }

            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Member {member.Name} {member.Surname} added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditMemberAsync(
            int memberId, string name, string surname, DateTime? birthdate,
            DateTime? anniversary, string? phoneNumber, int? familyId, IFormFile? photo)
        {
            // FindAsync looks up a record by primary key. Returns null if not found.
            var member = await _db.Members.FindAsync(memberId);
            if (member == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            {
                TempData["Error"] = "Name and surname are required.";
                return RedirectToPage();
            }

            // Update the scalar fields
            member.Name = name.Trim();
            member.Surname = surname.Trim();
            member.Birthdate = birthdate;
            member.Anniversary = anniversary;
            member.PhoneNumber = phoneNumber?.Trim();
            member.FamilyId = familyId;

            // Only replace the photo if the user actually uploaded a new one
            if (photo != null && photo.Length > 0)
            {
                var error = ValidatePhoto(photo);
                if (error != null) { TempData["Error"] = error; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Not a valid image."; return RedirectToPage(); }

                // Delete the old photo file from disk so we don't accumulate orphaned files
                if (!string.IsNullOrEmpty(member.PhotoFileName))
                {
                    var oldPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", member.PhotoFileName);
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                member.PhotoFileName = await SavePhotoAsync(photo);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Member {member.Name} {member.Surname} updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMemberAsync(int memberId)
        {
            var m = await _db.Members.FindAsync(memberId);
            if (m != null)
            {
                // Delete the photo file from disk when the member is deleted
                if (!string.IsNullOrEmpty(m.PhotoFileName))
                {
                    var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                    var filePath = Path.Combine(uploads, m.PhotoFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                _db.Members.Remove(m);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Member {m.Name} {m.Surname} deleted.";
            }
            return RedirectToPage();
        }
    }
}

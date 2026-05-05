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

        public AdminModel(DirectoryContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<Family> Families { get; set; } = new();
        public List<Member> Members { get; set; } = new();

        public async Task OnGetAsync()
        {
            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await _db.Members.Include(m => m.Family).OrderBy(m => m.Surname).ToListAsync();
        }

        public async Task<IActionResult> OnPostAddFamilyAsync(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName)) return RedirectToPage();
            _db.Families.Add(new Family { FamilyName = familyName.Trim() });
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteFamilyAsync(int familyId)
        {
            var f = await _db.Families.FindAsync(familyId);
            if (f != null)
            {
                _db.Families.Remove(f);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddMemberAsync(string name, string surname, DateTime? birthdate, DateTime? anniversary, string? phoneNumber, int? familyId, IFormFile? photo)
        {
            var member = new Member
            {
                Name = name ?? string.Empty,
                Surname = surname ?? string.Empty,
                Birthdate = birthdate,
                Anniversary = anniversary,
                PhoneNumber = phoneNumber,
                FamilyId = familyId
            };

            if (photo != null && photo.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using var fs = System.IO.File.Create(filePath);
                await photo.CopyToAsync(fs);
                member.PhotoFileName = fileName;
            }

            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMemberAsync(int memberId)
        {
            var m = await _db.Members.FindAsync(memberId);
            if (m != null)
            {
                if (!string.IsNullOrEmpty(m.PhotoFileName))
                {
                    var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                    var filePath = Path.Combine(uploads, m.PhotoFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                _db.Members.Remove(m);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}

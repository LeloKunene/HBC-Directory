using HBCDirectory.Data;
using HBCDirectory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    [Authorize(Roles = "Admin")]
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

        private static string CapitalizeFirst(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        public async Task OnGetAsync()
        {
            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await _db.Members.Include(m => m.Family).OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostAddFamilyAsync(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            {
                TempData["Error"] = "Family name cannot be empty.";
                return RedirectToPage();
            }
            _db.Families.Add(new Family { FamilyName = CapitalizeFirst(familyName.Trim()) });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Family '{familyName.Trim()}' successfully added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteFamilyAsync(int familyId)
        {
            var family = await _db.Families.FindAsync(familyId);
            var familyName = family.FamilyName;

            if (family != null)
            {
                
                _db.Families.Remove(family);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Family '{familyName}' successfully deleted.";
            }
            else
            {
                TempData["Error"] = $"Could not delete family '{familyName}'.";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddMemberAsync(string name, string surname, DateTime? birthdate, DateTime? anniversary, string phoneNumber, int? familyId, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname)
                || !birthdate.HasValue || string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "Could not add member — name, surname, birthdate, and phone number are all required.";
                return RedirectToPage();
            }

            try
            {
                var member = new Member
                {
                    Name = CapitalizeFirst(name.Trim()),
                    Surname = CapitalizeFirst(surname.Trim()),
                    Birthdate = birthdate,
                    Anniversary = anniversary,
                    PhoneNumber = phoneNumber.Trim(),
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
                TempData["Success"] = $"Member '{name} {surname}' successfully added";
            }
            catch (Exception)
            {
                TempData["Error"] = $"Could not add member '{name} {surname}' — something went wrong. Please try again.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMemberAsync(int memberId)
        {
            var member = await _db.Members.FindAsync(memberId);
            var memberName = member.Name;
            var memberSurname = member.Surname;

            if (member != null)
            {
                if (!string.IsNullOrEmpty(member.PhotoFileName))
                {
                    var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                    var filePath = Path.Combine(uploads, member.PhotoFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                _db.Members.Remove(member);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Member '{memberName} {memberSurname}' successfully deleted";
            }
            else
            {
                TempData["Error"] = $"Could not delete member '{memberName} {memberSurname}'.";
            }
            return RedirectToPage();
        }
    }
}

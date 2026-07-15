using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
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
        private readonly PhotoService _photos;
        private readonly IConfiguration _config;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };
        private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

        public static readonly string[] AllowedRoles = { "Member", "Deacon", "Elder" };

        public AdminModel(DirectoryContext db, IConfiguration config, PhotoService photos)
        {
            _db = db;
            _config = config;
            _photos = photos;
        }

        public string PhotoUrl(string? fileName) => _photos.Url(fileName);

        public List<Family> Families { get; set; } = new();
        public List<Member> Members { get; set; } = new();

        private static string CapitalizeFirst(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }
        [BindProperty(SupportsGet = true)]
        public int? EditMemberId { get; set; }
        public Member? EditingMember { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? EditFamilyId { get; set; }
        public Family? EditingFamily { get; set; }

        public async Task OnGetAsync()
        {
            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await _db.Members.Include(m => m.Family).OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();

            if (EditMemberId.HasValue)
                EditingMember = Members.FirstOrDefault(m => m.Id == EditMemberId.Value);

            if (EditFamilyId.HasValue)
                EditingFamily = Families.FirstOrDefault(f => f.Id == EditFamilyId.Value);
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
            var fileName = Guid.NewGuid().ToString() + 
                        Path.GetExtension(photo.FileName).ToLowerInvariant();

            var credentials = new BasicAWSCredentials(
                _config["R2:AccessKeyId"],
                _config["R2:SecretAccessKey"]);

            var config = new AmazonS3Config
            {
                ServiceURL = _config["R2:Endpoint"],
                ForcePathStyle = true
            };

            using var client = new AmazonS3Client(credentials, config);
            using var stream = photo.OpenReadStream();

            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _config["R2:BucketName"],
                Key = fileName,
                InputStream = stream,
                ContentType = photo.ContentType,
                DisablePayloadSigning = true
            });

            return fileName;
        }

        private async Task DeletePhotoAsync(string fileName)
        {
            try
            {
                var credentials = new BasicAWSCredentials(
                    _config["R2:AccessKeyId"],
                    _config["R2:SecretAccessKey"]);

                var config = new AmazonS3Config
                {
                    ServiceURL = _config["R2:Endpoint"],
                    ForcePathStyle = true
                };

                using var client = new AmazonS3Client(credentials, config);

                await client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _config["R2:BucketName"],
                    Key = fileName
                });

                Console.WriteLine($"Deleted from R2: {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"R2 delete failed for {fileName}: {ex.Message}");
            }
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

        public async Task<IActionResult> OnPostAddMemberAsync(
            string name, string surname, DateTime? birthdate, string? role,
            DateTime? anniversary, string phoneNumber, int? familyId, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(surname) ||
                !birthdate.HasValue ||
                string.IsNullOrWhiteSpace(phoneNumber))
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
                    FamilyId = familyId,
                    Role = string.IsNullOrEmpty(role) ? null : role,
                };

                if (photo != null && photo.Length > 0)
                {
                    var error = ValidatePhoto(photo);
                    if (error != null)
                    {
                        TempData["Error"] = error;
                        return RedirectToPage();
                    }

                    if (!await IsImageAsync(photo))
                    {
                        TempData["Error"] = "Not a valid image.";
                        return RedirectToPage();
                    }

                    member.PhotoFileName = await SavePhotoAsync(photo);
                }

                _db.Members.Add(member);
                await _db.SaveChangesAsync();

                TempData["Success"] = $"Member '{member.Name} {member.Surname}' successfully added";
            }
            catch (Exception)
            {
                TempData["Error"] = $"Could not add member '{name} {surname}' — something went wrong. Please try again.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditMemberAsync(
            int memberId, string name, string surname, DateTime? birthdate, string? role,
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
            member.Role = string.IsNullOrEmpty(role) ? null : role;

            // Only replace the photo if the user actually uploaded a new one
            if (photo != null && photo.Length > 0)
            {
                var error = ValidatePhoto(photo);
                if (error != null) { TempData["Error"] = error; return RedirectToPage(); }
                if (!await IsImageAsync(photo)) { TempData["Error"] = "Not a valid image."; return RedirectToPage(); }

                // Delete the old photo file from disk so we don't accumulate orphaned files
                if (!string.IsNullOrEmpty(member.PhotoFileName))
                    await DeletePhotoAsync(member.PhotoFileName);

                member.PhotoFileName = await SavePhotoAsync(photo);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Member {member.Name} {member.Surname} updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMemberAsync(int memberId)
        {
            var member = await _db.Members.FindAsync(memberId);
            var memberName = member.Name;
            var memberSurname = member.Surname;

            if (member != null)
            {
                // Delete the photo file from disk when the member is deleted
                if (!string.IsNullOrEmpty(member.PhotoFileName))  
                {
                    await DeletePhotoAsync(member.PhotoFileName);
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

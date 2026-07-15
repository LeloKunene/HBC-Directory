using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HBCDirectory.Data;
using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HBCDirectory.Pages.Profile
{
    [Authorize(Roles = "Member,Admin")]
    public class IndexModel : PageModel
    {
        private readonly DirectoryContext _db;
        private readonly IConfiguration _config;
        private readonly PhotoService _photos;

        public IndexModel(DirectoryContext db, IConfiguration config, PhotoService photos)
        {
            _db     = db;
            _config = config;
            _photos = photos;
        }

        public string PhotoUrl(string? fileName) => _photos.Url(fileName);
        public Member? CurrentMember { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var memberId = GetMemberId();

            if (memberId != null)
            {
                CurrentMember = await _db.Members.FindAsync(memberId.Value);
            }
            else
            {
                // Admin — look up by their username (email)
                var username = User.Identity?.Name;
                var account = await _db.MemberAccounts
                    .Include(a => a.Member)
                    .FirstOrDefaultAsync(a => a.Username == username);
                CurrentMember = account?.Member;
            }

            if (CurrentMember == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            string name, string surname, string? phoneNumber,
            bool showPhone, bool showBirthdate, bool showAnniversary,
            IFormFile? photo)
        {
            var memberId = GetMemberId();
            if (memberId == null) return RedirectToPage("/Login");

            var member = await _db.Members.FindAsync(memberId.Value);
            if (member == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            {
                TempData["Error"] = "Name and surname are required.";
                return RedirectToPage();
            }

            member.Name             = name.Trim();
            member.Surname          = surname.Trim();
            member.PhoneNumber      = phoneNumber?.Trim();
            member.ShowPhone        = showPhone;
            member.ShowBirthdate    = showBirthdate;
            member.ShowAnniversary  = showAnniversary;

            if (photo != null && photo.Length > 0)
            {
                var fileName    = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName).ToLowerInvariant();
                var credentials = new BasicAWSCredentials(_config["R2:AccessKeyId"], _config["R2:SecretAccessKey"]);
                var cfg         = new AmazonS3Config { ServiceURL = _config["R2:Endpoint"], ForcePathStyle = true };
                using var client = new AmazonS3Client(credentials, cfg);
                using var stream = photo.OpenReadStream();
                await client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName           = _config["R2:BucketName"],
                    Key                  = fileName,
                    InputStream          = stream,
                    ContentType          = photo.ContentType,
                    DisablePayloadSigning = true
                });
                member.PhotoFileName = fileName;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Profile updated successfully.";
            return RedirectToPage();
        }

        private int? GetMemberId()
        {
            var claim = User.FindFirst("MemberId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}

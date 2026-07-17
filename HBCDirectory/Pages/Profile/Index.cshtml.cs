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
using System.Text.Json;

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
        public bool HasPendingUpdate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var memberId = GetMemberId();

            if (memberId != null)
            {
                CurrentMember = await _db.Members.FindAsync(memberId.Value);
                // Check if there's a pending update waiting for approval
                HasPendingUpdate = await _db.PendingUpdates
                    .AnyAsync(p => p.MemberId == memberId.Value && !p.IsApproved && !p.IsRejected);
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

            // If there's already a pending update, reject the old one first
            var existingPending = await _db.PendingUpdates
                .FirstOrDefaultAsync(p => p.MemberId == memberId.Value && !p.IsApproved && !p.IsRejected);
            if (existingPending != null)
            {
                existingPending.IsRejected = true;
                existingPending.ReviewNote = "Superseded by a newer submission";
            }

            // Build the changes JSON
            var changes = new
            {
                name        = name.Trim(),
                surname     = surname.Trim(),
                phoneNumber = phoneNumber?.Trim(),
                showPhone,
                showBirthdate,
                showAnniversary
            };

            var pending = new PendingUpdate
            {
                MemberId    = memberId.Value,
                ChangesJson = JsonSerializer.Serialize(changes),
                SubmittedAt = DateTime.UtcNow
            };

            // Handle photo — store in R2 with a "pending-" prefix so it doesn't replace
            // the live photo until admin approves
            if (photo != null && photo.Length > 0)
            {
                var fileName    = "pending-" + Guid.NewGuid() + Path.GetExtension(photo.FileName).ToLowerInvariant();
                var credentials = new BasicAWSCredentials(_config["R2:AccessKeyId"], _config["R2:SecretAccessKey"]);
                var cfg         = new AmazonS3Config { ServiceURL = _config["R2:Endpoint"], ForcePathStyle = true };
                using var client = new AmazonS3Client(credentials, cfg);
                using var stream = photo.OpenReadStream();
                await client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName            = _config["R2:BucketName"],
                    Key                   = fileName,
                    InputStream           = stream,
                    ContentType           = photo.ContentType,
                    DisablePayloadSigning = true
                });
                pending.PendingPhotoFileName = fileName;
            }

            _db.PendingUpdates.Add(pending);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Your changes have been submitted and are pending admin review. They will appear in the directory once approved.";
            return RedirectToPage();
        }

        private int? GetMemberId()
        {
            var claim = User.FindFirst("MemberId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}

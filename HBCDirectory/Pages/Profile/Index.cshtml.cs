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
        public List<HBCDirectory.Models.MemberGroup> MemberGroups { get; set; } = new();
        public Family? MyFamily { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var memberId = GetMemberId();

            if (memberId != null)
            {
                CurrentMember = await _db.Members.FindAsync(memberId.Value);
                HasPendingUpdate = await _db.PendingUpdates
                    .AnyAsync(p => p.MemberId == memberId.Value && !p.IsApproved && !p.IsRejected);
            }
            else
            {
                var username = User.Identity?.Name;
                var account = await _db.MemberAccounts
                    .Include(a => a.Member)
                    .FirstOrDefaultAsync(a => a.Username == username);
                CurrentMember = account?.Member;
            }

            ApprovalConfig = await _db.ApprovalSettings.FindAsync(1) ?? new ApprovalSettings();

            if (memberId != null && CurrentMember != null)
            {
                MemberGroups = await _db.MemberGroups
                    .Include(mg => mg.Group)
                    .Where(mg => mg.MemberId == memberId.Value)
                    .ToListAsync();

                if (CurrentMember.FamilyId.HasValue)
                {
                    var family = await _db.Families.FindAsync(CurrentMember.FamilyId.Value);
                    if (family != null && family.HeadOfFamilyId == CurrentMember.Id)
                        MyFamily = family;
                }
            }

            if (CurrentMember == null) return NotFound();
            return Page();
        }

        public ApprovalSettings ApprovalConfig { get; set; } = new();

        public async Task<IActionResult> OnPostAsync(
            string name, string surname, string? phoneNumber, string? address,
            bool showPhone, bool showAddress,
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

            // Load approval settings (one row, always Id = 1)
            var settings = await _db.ApprovalSettings.FindAsync(1)
                           ?? new ApprovalSettings();

            // ── Apply immediate changes (no approval needed) ─────────────
            if (!settings.RequireApprovalForName)
            {
                member.Name    = name.Trim();
                member.Surname = surname.Trim();
            }
            if (!settings.RequireApprovalForPhone)
            {
                member.PhoneNumber = phoneNumber?.Trim();
                member.Address     = address?.Trim();
            }
            if (!settings.RequireApprovalForPrivacy)
            {
                member.ShowPhone   = showPhone;
                member.ShowAddress = showAddress;
            }
            await _db.SaveChangesAsync();

            // ── Build pending changes (fields that need approval) ─────────
            var pendingFields = new Dictionary<string, object?>();
            if (settings.RequireApprovalForName)
            {
                pendingFields["name"]    = name.Trim();
                pendingFields["surname"] = surname.Trim();
            }
            if (settings.RequireApprovalForPhone)
            {
                pendingFields["phoneNumber"] = phoneNumber?.Trim();
                pendingFields["address"]     = address?.Trim();
            }
            if (settings.RequireApprovalForPrivacy)
            {
                pendingFields["showPhone"]   = showPhone;
                pendingFields["showAddress"] = showAddress;
            }

            bool hasPhoto = photo != null && photo.Length > 0;
            bool hasPendingChanges = pendingFields.Count > 0 || (hasPhoto && settings.RequireApprovalForPhoto);

            if (hasPendingChanges)
            {
                // Supersede any existing pending update
                var existing = await _db.PendingUpdates
                    .FirstOrDefaultAsync(p => p.MemberId == memberId.Value && !p.IsApproved && !p.IsRejected);
                if (existing != null)
                {
                    existing.IsRejected = true;
                    existing.ReviewNote = "Superseded by a newer submission";
                }

                var pending = new PendingUpdate
                {
                    MemberId    = memberId.Value,
                    ChangesJson = JsonSerializer.Serialize(pendingFields),
                    SubmittedAt = DateTime.UtcNow
                };

                // Photo — store with "pending-" prefix until approved
                if (hasPhoto && settings.RequireApprovalForPhoto)
                {
                    var fileName    = "pending-" + Guid.NewGuid() + Path.GetExtension(photo!.FileName).ToLowerInvariant();
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

                TempData["Success"] = "Some changes were applied immediately. Others (marked below) have been submitted for admin review.";
            }
            else
            {
                // Photo not requiring approval — apply it directly
                if (hasPhoto)
                {
                    var fileName    = Guid.NewGuid() + Path.GetExtension(photo!.FileName).ToLowerInvariant();
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
                    member.PhotoFileName = fileName;
                    await _db.SaveChangesAsync();
                }

                TempData["Success"] = "Profile updated successfully.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateFamilyPhotoAsync(IFormFile? familyPhoto)
        {
            var memberId = GetMemberId();
            if (memberId == null) return RedirectToPage("/Login");

            var member = await _db.Members.FindAsync(memberId.Value);
            if (member == null || !member.FamilyId.HasValue) return NotFound();

            var family = await _db.Families.FindAsync(member.FamilyId.Value);
            if (family == null) return NotFound();

            if (family.HeadOfFamilyId != member.Id)
            {
                TempData["Error"] = "Only the head of your family can change the family photo.";
                return RedirectToPage();
            }

            if (familyPhoto == null || familyPhoto.Length == 0)
            {
                TempData["Error"] = "Choose a photo to upload.";
                return RedirectToPage();
            }

            var credentials = new BasicAWSCredentials(_config["R2:AccessKeyId"], _config["R2:SecretAccessKey"]);
            var s3Config    = new AmazonS3Config { ServiceURL = _config["R2:Endpoint"], ForcePathStyle = true };
            using var client = new AmazonS3Client(credentials, s3Config);

            if (!string.IsNullOrEmpty(family.PhotoFileName))
            {
                try { await client.DeleteObjectAsync(_config["R2:BucketName"], family.PhotoFileName); }
                catch { }
            }

            var fileName = Guid.NewGuid() + Path.GetExtension(familyPhoto.FileName).ToLowerInvariant();
            using var stream = familyPhoto.OpenReadStream();
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName            = _config["R2:BucketName"],
                Key                   = fileName,
                InputStream           = stream,
                ContentType           = familyPhoto.ContentType,
                DisablePayloadSigning = true
            });

            family.PhotoFileName = fileName;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"'{family.FamilyName}' family photo updated.";
            return RedirectToPage();
        }

        private int? GetMemberId()
        {
            var claim = User.FindFirst("MemberId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}

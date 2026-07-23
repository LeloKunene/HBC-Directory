namespace HBCDirectory.Models
{
    public class PendingFamilyPhoto
    {
        public int Id { get; set; }
        public int FamilyId { get; set; }
        public Family Family { get; set; } = null!;
        public string PendingPhotoFileName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public bool IsApproved { get; set; } = false;
        public bool IsRejected { get; set; } = false;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
    }
}

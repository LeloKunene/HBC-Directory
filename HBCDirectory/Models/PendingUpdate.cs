namespace HBCDirectory.Models
{
    public class PendingUpdate
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public string ChangesJson { get; set; } = "{}";
        public string? PendingPhotoFileName { get; set; }

        public bool IsApproved { get; set; } = false;
        public bool IsRejected { get; set; } = false;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
    }
}

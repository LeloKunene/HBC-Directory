namespace HBCDirectory.Models
{
    public class IssueReport
    {
        public int Id { get; set; }
        public string Category { get; set; } = "Bug"; // "Bug" | "Suggestion" | "Other"
        public string Description { get; set; } = string.Empty;

        public string? PageUrl { get; set; }
        public string? UserAgent { get; set; }
        public int? ReportedByMemberId { get; set; }
        public Member? ReportedByMember { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; } = false;
        public DateTime? ResolvedAt { get; set; }
    }
}

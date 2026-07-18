namespace HBCDirectory.Models
{
    public class ApprovalSettings
    {
        public int Id { get; set; } = 1;
        public bool RequireApprovalForName    { get; set; } = false;
        public bool RequireApprovalForPhone   { get; set; } = false;
        public bool RequireApprovalForPrivacy { get; set; } = false;
        public bool RequireApprovalForPhoto   { get; set; } = true;
    }
}

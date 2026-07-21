namespace HBCDirectory.Models
{
    public class MemberRole
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }
}

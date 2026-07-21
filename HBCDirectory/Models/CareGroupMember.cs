namespace HBCDirectory.Models
{
    public class CareGroupMember
    {
        public int Id { get; set; }
        public int CareGroupId { get; set; }
        public CareGroup CareGroup { get; set; } = null!;
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;
    }
}

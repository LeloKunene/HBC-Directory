namespace HBCDirectory.Models
{
    public class MemberGroup
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;
        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;
    }
}

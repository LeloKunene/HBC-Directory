namespace HBCDirectory.Models
{
    public class GroupLeader
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;
    }
}

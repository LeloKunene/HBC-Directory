namespace HBCDirectory.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public ICollection<MemberGroup> MemberGroups { get; set; } = new List<MemberGroup>();
    }
}

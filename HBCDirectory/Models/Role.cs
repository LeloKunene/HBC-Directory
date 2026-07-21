namespace HBCDirectory.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public ICollection<MemberRole> MemberRoles { get; set; } = new List<MemberRole>();
    }
}

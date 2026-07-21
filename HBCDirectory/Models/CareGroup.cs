namespace HBCDirectory.Models
{
    public class CareGroup
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<CareGroupLeader> Leaders { get; set; } = new List<CareGroupLeader>();
        public ICollection<CareGroupMember> Members { get; set; } = new List<CareGroupMember>();
    }
}

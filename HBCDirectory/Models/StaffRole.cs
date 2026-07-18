namespace HBCDirectory.Models
{
    /// <summary>
    /// Admin-configurable staff roles.
    /// Current HBC roles: Campus Worker, Secretary, Pastoral Intern.
    /// Pastor = Elder (not a staff role).
    /// Admin can add/rename/delete roles without code changes.
    /// </summary>
    public class StaffRole
    {
        public int Id { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        public ICollection<StaffAssignment> Assignments { get; set; } = new List<StaffAssignment>();
    }
}

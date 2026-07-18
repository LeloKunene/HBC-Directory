namespace HBCDirectory.Models
{
    /// <summary>
    /// Links a Member to a staff role
    /// Staff appear under their family in the main directory.
    /// Staff appear in a dedicated section near the front of the printed PDF.
    /// </summary>
    public class StaffAssignment
    {
        public int Id { get; set; }

        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;

        public int StaffRoleId { get; set; }
        public StaffRole StaffRole { get; set; } = null!;

        public string? Bio { get; set; }
        public int DisplayOrder { get; set; }
    }
}

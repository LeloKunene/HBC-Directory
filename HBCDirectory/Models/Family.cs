using System.ComponentModel.DataAnnotations.Schema;

namespace HBCDirectory.Models
{
    public class Family
    {
        public int Id { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public string? PhotoFileName { get; set; }
        public string? Address { get; set; }
        public string? FamilyPhone { get; set; }
        public string? AdditionalNotes { get; set; }

        public ICollection<Member> Members { get; set; } = new List<Member>();

        public int? HeadOfFamilyId { get; set; }
        public Member? HeadOfFamily { get; set; }

        [NotMapped]
        public IEnumerable<Member> Adults =>
            Members.Where(m => m.MemberType == "Adult" && Member.IsVisibleToCongregation(m))
                   .OrderBy(m => m.Surname).ThenBy(m => m.Name);

        [NotMapped]
        public IEnumerable<Member> Children =>
            Members.Where(m => m.MemberType == "Child")
                   .OrderBy(m => m.Birthdate);

        [NotMapped]
        public bool HasLeadership =>
            Members.Any(m => m.ChurchOffice is "Elder" or "Deacon");
    }
}

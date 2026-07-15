using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HBCDirectory.Models
{
    public class Family
    {
        public int Id { get; set; }

        [Required]
        public string FamilyName { get; set; } = string.Empty;

        // Navigation property, lets EF Core load members with Include()
        public ICollection<Member> Members { get; set; } = new List<Member>();

        // Family group photo stored in R2
        public string? PhotoFileName { get; set; }

    }
}

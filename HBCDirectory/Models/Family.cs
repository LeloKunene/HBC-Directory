using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HBCDirectory.Models
{
    public class Family
    {
        public int Id { get; set; }

        [Required]
        public string FamilyName { get; set; } = string.Empty;

        public List<Member> Members { get; set; } = new();
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HBCDirectory.Models
{
    public class Member
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Surname { get; set; } = string.Empty;

        public DateTime? Birthdate { get; set; }

        public DateTime? Anniversary { get; set; }

        public string? PhoneNumber { get; set; }

        // Stored filename in wwwroot/uploads
        public string? PhotoFileName { get; set; }

        // Foreign key
        public int? FamilyId { get; set; }
        public Family? Family { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HBCDirectory.Models
{
    /// <summary>
    /* Represents a person in the HBC directory.
        MemberType   — structural: "Adult" | "Child" (manual, staff decide)
        MemberStatus — relational: "Member" | "Attendant" | null (null for children)
        ChurchOffice — office: "Elder" | "Deacon" | null
                        Only valid when MemberStatus = "Member".
                        Removed when a person steps down.
        
        Email is required for Adults (they get a login account).
        Email is null for Children (no login account created).*/
    /// </summary>
    public class Member
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string? Email { get; set; }

        // Type & Status
        public string MemberType { get; set; } = "Adult"; // "Adult" | "Child"
        public string? MemberStatus { get; set; } = "Member";
        public string? ChurchOffice { get; set; } // "Elder" | "Deacon" | null

        public DateTime? Birthdate { get; set; }
        public DateTime? Anniversary { get; set; }
        public DateTime? DateJoined   { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address     { get; set; }
        public string? PhotoFileName { get; set; }

        public int? FamilyId { get; set; }
        public Family? Family { get; set; }

        public bool ShowPhone { get; set; } = true;
        public bool ShowAddress    { get; set; } = true;
        public bool ShowBirthdate { get; set; } = true;
        public bool ShowAnniversary { get; set; } = true;

        // Helpers
        public bool IsAdult => MemberType == "Adult";
        public bool IsChild => MemberType == "Child";
        public bool IsLeadership => ChurchOffice is "Elder" or "Deacon";
        public string DisplayName => $"{Name} {Surname}".Trim();
    }
}


using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HBCDirectory.Models
{
    /// <summary>
    /* Represents a person in the HBC directory.
        MemberType   — structural: "Adult" | "Child" (manual, staff decide)
        MemberStatus — "Member" | "Attendant" | "Pending Removal" |
                        "Pending Discipline" | "Resigned" | "Excommunicated" |
                        null (null for children).
                        The last four are Leadership-managed via Member
                        Management, not the regular Admin Edit Member form —
                        see IsVisibleToCongregation/PublicStatus below for
                        what each means for directory visibility.
        ChurchOffice — office: "Elder" | "Deacon" | null
                        Only valid when MemberStatus = "Member".
                        Removed when a person steps down.
        
        Email is required for Adults with MemberStatus "Member" (they get a
        login account). Email is null for Children (no login account
        created).*/
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

        // Resigned/Excommunicated members are no longer part of the visible
        // congregation thus excluded from the public directory, family modal,
        // PDF export, and birthday/anniversary notifications. Still fully
        // visible to Admin/Leadership (Member Management, Admin's own
        // member list) —> this only controls congregation-facing views.
        //
        // Pending Removal/Pending Discipline are deliberately NOT excluded
        // here, per the agreed design, someone under a still-open process
        // stays visible as an ordinary member card. Only their status LABEL
        // is sensitive, not their presence in the directory.
        //
        // EF Core can't translate a call to this method into SQL —> actual
        // database queries (Index.cshtml.cs's Load*Async methods, Admin's
        // PDF generation) repeat the same two comparisons inline instead of
        // calling this. Keep them in sync if this list ever changes.
        public static bool IsVisibleToCongregation(Member m) =>
            m.MemberStatus != "Resigned" && m.MemberStatus != "Excommunicated";

        // What a regular member is allowed to see of someone's status
        // "Member"/"Attendant" pass through as-is (used for the existing
        // Members/Attendants filter and search), anything else (Pending
        // Removal, Pending Discipline, the only other statuses that can
        // reach this, since Resigned/Excommunicated are filtered out of the
        // congregation-facing views entirely) comes back null. This exists
        // so a discipline status never leaks into member-facing HTML at
        // all. Not as visible text, and not into data-status/data-search
        // attributes either, which a person could still read via browser
        // dev tools even though nothing renders it on screen.
        public static string? PublicStatus(string? status) =>
            status is "Member" or "Attendant" ? status : null;
    }
}


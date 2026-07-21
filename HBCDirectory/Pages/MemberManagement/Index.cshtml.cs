using HBCDirectory.Data;
using HBCDirectory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages.MemberManagement
{
    [Authorize(Roles = "Leadership,SystemAdmin")]
    public class IndexModel : PageModel
    {
        private readonly DirectoryContext _db;

        public IndexModel(DirectoryContext db) { _db = db; }

        public List<Role> Roles { get; set; } = new();
        public List<Member> AllMembers { get; set; } = new();
        public List<MemberRole> Assignments { get; set; } = new();

        public async Task OnGetAsync()
        {
            Roles = await _db.Roles.OrderBy(r => r.DisplayOrder).ToListAsync();
            AllMembers = await _db.Members
                .Where(m => m.MemberType == "Adult")
                .OrderBy(m => m.Surname).ThenBy(m => m.Name)
                .ToListAsync();
            Assignments = await _db.MemberRoles
                .Include(mr => mr.Member).Include(mr => mr.Role)
                .OrderBy(mr => mr.Role.DisplayOrder).ThenBy(mr => mr.Member.Surname)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            { TempData["Error"] = "Role name is required."; return RedirectToPage(); }

            var trimmed = roleName.Trim();
            if (await _db.Roles.AnyAsync(r => r.Name == trimmed))
            { TempData["Error"] = $"A role named '{trimmed}' already exists."; return RedirectToPage(); }

            var maxOrder = await _db.Roles.AnyAsync() ? await _db.Roles.MaxAsync(r => r.DisplayOrder) : 0;
            _db.Roles.Add(new Role { Name = trimmed, DisplayOrder = maxOrder + 1 });
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Role '{trimmed}' created. Note: creating a role only adds a label — " +
                "it doesn't grant any special access on its own unless that's separately built into the app.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAssignRoleAsync(int memberId, int roleId)
        {
            var member = await _db.Members.FindAsync(memberId);
            var role   = await _db.Roles.FindAsync(roleId);
            if (member == null || role == null)
            { TempData["Error"] = "Member or role not found."; return RedirectToPage(); }

            if (await _db.MemberRoles.AnyAsync(mr => mr.MemberId == memberId && mr.RoleId == roleId))
            { TempData["Error"] = $"'{member.DisplayName}' already has the '{role.Name}' role."; return RedirectToPage(); }

            _db.MemberRoles.Add(new MemberRole { MemberId = memberId, RoleId = roleId });
            await _db.SaveChangesAsync();
            await LogChangeAsync("MemberRole", member.Id, member.DisplayName, "Assigned", $"Role: {role.Name}");

            TempData["Success"] = $"'{role.Name}' granted to '{member.DisplayName}'.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRevokeRoleAsync(int id)
        {
            var assignment = await _db.MemberRoles
                .Include(mr => mr.Member).Include(mr => mr.Role)
                .FirstOrDefaultAsync(mr => mr.Id == id);
            if (assignment == null) return NotFound();

            if (assignment.Role.Name == "Leadership")
            {
                var leadershipCount = await _db.MemberRoles.CountAsync(mr => mr.RoleId == assignment.RoleId);
                if (leadershipCount <= 1)
                {
                    TempData["Error"] = "Can't remove the last Leadership member — " +
                        "grant Leadership to someone else first.";
                    return RedirectToPage();
                }
            }

            _db.MemberRoles.Remove(assignment);
            await _db.SaveChangesAsync();
            await LogChangeAsync("MemberRole", assignment.Member.Id, assignment.Member.DisplayName,
                "Revoked", $"Role: {assignment.Role.Name}");

            TempData["Success"] = $"'{assignment.Role.Name}' removed from '{assignment.Member.DisplayName}'.";
            return RedirectToPage();
        }

        private async Task LogChangeAsync(
            string entityType, int entityId, string entityName, string action, string? notes = null)
        {
            _db.ChangeLogs.Add(new ChangeLog
            {
                ChangedAt  = DateTime.UtcNow,
                ChangedBy  = User.Identity?.Name ?? "unknown",
                EntityType = entityType,
                EntityId   = entityId,
                EntityName = entityName,
                Action     = action,
                Notes      = notes
            });
            await _db.SaveChangesAsync();
        }
    }
}

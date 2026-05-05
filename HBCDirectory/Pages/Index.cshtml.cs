using HBCDirectory.Data;
using HBCDirectory.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Pages
{
    public class IndexModel : PageModel
    {
        private readonly DirectoryContext _db;

        public IndexModel(DirectoryContext db)
        {
            _db = db;
        }

        public List<Family> Families { get; set; } = new();
        public List<Member> Members { get; set; } = new();

        public async Task OnGetAsync(int? familyId, string? q)
        {
            var membersQuery = _db.Members.Include(m => m.Family).AsQueryable();
            if (familyId.HasValue)
            {
                membersQuery = membersQuery.Where(m => m.FamilyId == familyId.Value);
            }
            if (!string.IsNullOrEmpty(q))
            {
                membersQuery = membersQuery.Where(m => m.Name.Contains(q) || m.Surname.Contains(q));
            }

            Families = await _db.Families.OrderBy(f => f.FamilyName).ToListAsync();
            Members = await membersQuery.OrderBy(m => m.Surname).ThenBy(m => m.Name).ToListAsync();
        }
    }
}

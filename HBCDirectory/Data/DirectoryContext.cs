using HBCDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Data
{
    public class DirectoryContext : DbContext
    {
        public DirectoryContext(DbContextOptions<DirectoryContext> options) : base(options)
        {
        }

        public DbSet<Family> Families { get; set; }
        public DbSet<Member> Members { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Family>()
                .HasMany(f => f.Members)
                .WithOne(m => m.Family)
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

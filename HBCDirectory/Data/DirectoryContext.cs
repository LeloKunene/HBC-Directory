using HBCDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Data
{
    public class DirectoryContext : DbContext
    {
        public DirectoryContext(DbContextOptions<DirectoryContext> options) : base(options) { }

        public DbSet<Family> Families => Set<Family>();
        public DbSet<Member> Members => Set<Member>();
        public DbSet<MemberAccount> MemberAccounts => Set<MemberAccount>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Deleting a Family cascade-deletes all its Members
            modelBuilder.Entity<Member>()
                .HasOne(m => m.Family)
                .WithMany(f => f.Members)
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a Member cascade-deletes their account
            modelBuilder.Entity<MemberAccount>()
                .HasOne(a => a.Member)
                .WithMany()
                .HasForeignKey(a => a.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            // One account per member
            modelBuilder.Entity<MemberAccount>()
                .HasIndex(a => a.MemberId)
                .IsUnique();

            // Usernames (emails) must be unique
            modelBuilder.Entity<MemberAccount>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // Member emails must be unique
            modelBuilder.Entity<Member>()
                .HasIndex(m => m.Email)
                .IsUnique();
        }
    }
}

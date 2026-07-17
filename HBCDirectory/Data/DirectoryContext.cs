using HBCDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Data
{
    public class DirectoryContext : DbContext
    {
        public DirectoryContext(DbContextOptions<DirectoryContext> options) : base(options) { }

        public DbSet<Family>           Families           => Set<Family>();
        public DbSet<Member>           Members            => Set<Member>();
        public DbSet<MemberAccount>    MemberAccounts     => Set<MemberAccount>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<StaffRole>        StaffRoles         => Set<StaffRole>();
        public DbSet<StaffAssignment>  StaffAssignments   => Set<StaffAssignment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Family → Members (deleting a family deletes all its members)
            modelBuilder.Entity<Member>()
                .HasOne(m => m.Family)
                .WithMany(f => f.Members)
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Member → MemberAccount (deleting a member deletes their account)
            modelBuilder.Entity<MemberAccount>()
                .HasOne(a => a.Member)
                .WithMany()
                .HasForeignKey(a => a.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            // One account per member
            modelBuilder.Entity<MemberAccount>()
                .HasIndex(a => a.MemberId)
                .IsUnique();

            // Usernames (emails) must be unique across accounts
            modelBuilder.Entity<MemberAccount>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // StaffAssignment → Member
            modelBuilder.Entity<StaffAssignment>()
                .HasOne(sa => sa.Member)
                .WithMany()
                .HasForeignKey(sa => sa.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            // StaffAssignment → StaffRole
            modelBuilder.Entity<StaffAssignment>()
                .HasOne(sa => sa.StaffRole)
                .WithMany(sr => sr.Assignments)
                .HasForeignKey(sa => sa.StaffRoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

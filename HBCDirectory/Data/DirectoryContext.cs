using HBCDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace HBCDirectory.Data
{
    public class DirectoryContext : DbContext
    {
        public DirectoryContext(DbContextOptions<DirectoryContext> options) : base(options) { }

        public DbSet<Family>            Families           => Set<Family>();
        public DbSet<Member>            Members            => Set<Member>();
        public DbSet<MemberAccount>     MemberAccounts     => Set<MemberAccount>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<StaffRole>         StaffRoles         => Set<StaffRole>();
        public DbSet<StaffAssignment>   StaffAssignments   => Set<StaffAssignment>();
        public DbSet<Group>             Groups             => Set<Group>();
        public DbSet<MemberGroup>       MemberGroups       => Set<MemberGroup>();
        public DbSet<PendingUpdate>     PendingUpdates     => Set<PendingUpdate>();
        public DbSet<ChangeLog>         ChangeLogs         => Set<ChangeLog>();
        public DbSet<ApprovalSettings>  ApprovalSettings   => Set<ApprovalSettings>();
        public DbSet<PdfSettings> PdfSettings => Set<PdfSettings>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Member>()
                .HasOne(m => m.Family).WithMany(f => f.Members)
                .HasForeignKey(m => m.FamilyId).OnDelete(DeleteBehavior.Cascade);

            // A second relationship between the same two tables (Family "points
            // at" one of its own Members as the head) — can't also be Cascade,
            // or Postgres/EF rejects the model with multiple cascade paths
            // between Family and Member. SetNull instead: if the designated
            // head is ever removed as a member, the family just loses its head
            // designation rather than the deletion being blocked.
            modelBuilder.Entity<Family>()
                .HasOne(f => f.HeadOfFamily).WithMany()
                .HasForeignKey(f => f.HeadOfFamilyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MemberAccount>()
                .HasOne(a => a.Member).WithMany()
                .HasForeignKey(a => a.MemberId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MemberAccount>().HasIndex(a => a.MemberId).IsUnique();
            modelBuilder.Entity<MemberAccount>().HasIndex(a => a.Username).IsUnique();

            modelBuilder.Entity<StaffAssignment>()
                .HasOne(sa => sa.Member).WithMany()
                .HasForeignKey(sa => sa.MemberId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StaffAssignment>()
                .HasOne(sa => sa.StaffRole).WithMany(sr => sr.Assignments)
                .HasForeignKey(sa => sa.StaffRoleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MemberGroup>()
                .HasOne(mg => mg.Member).WithMany()
                .HasForeignKey(mg => mg.MemberId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MemberGroup>()
                .HasOne(mg => mg.Group).WithMany(g => g.MemberGroups)
                .HasForeignKey(mg => mg.GroupId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MemberGroup>()
                .HasIndex(mg => new { mg.MemberId, mg.GroupId }).IsUnique();

            modelBuilder.Entity<PendingUpdate>()
                .HasOne(p => p.Member).WithMany()
                .HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}

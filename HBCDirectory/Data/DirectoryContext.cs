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
        public DbSet<PendingFamilyPhoto> PendingFamilyPhotos => Set<PendingFamilyPhoto>();
        public DbSet<ChangeLog>         ChangeLogs         => Set<ChangeLog>();
        public DbSet<ApprovalSettings>  ApprovalSettings   => Set<ApprovalSettings>();
        public DbSet<PdfSettings> PdfSettings => Set<PdfSettings>();
        public DbSet<Role>       Roles       => Set<Role>();
        public DbSet<MemberRole> MemberRoles => Set<MemberRole>();
        public DbSet<CareGroup>       CareGroups       => Set<CareGroup>();
        public DbSet<CareGroupLeader> CareGroupLeaders => Set<CareGroupLeader>();
        public DbSet<CareGroupMember> CareGroupMembers => Set<CareGroupMember>();

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

            modelBuilder.Entity<PendingFamilyPhoto>()
                .HasOne(p => p.Family).WithMany()
                .HasForeignKey(p => p.FamilyId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MemberRole>()
                .HasOne(mr => mr.Member).WithMany()
                .HasForeignKey(mr => mr.MemberId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MemberRole>()
                .HasOne(mr => mr.Role).WithMany(r => r.MemberRoles)
                .HasForeignKey(mr => mr.RoleId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MemberRole>()
                .HasIndex(mr => new { mr.MemberId, mr.RoleId }).IsUnique();

            // "Admin" and "Leadership" are seeded with fixed IDs because their
            // exact Name strings are checked by [Authorize(Roles = "...")]
            // throughout the app — they need to exist with the right spelling
            // out of the box, not depend on an admin typing them correctly by
            // hand. Any further roles created later through Member Management
            // are plain labels with no special behavior unless separately
            // wired up in code.
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin",      DisplayOrder = 1 },
                new Role { Id = 2, Name = "Leadership", DisplayOrder = 2 }
            );

            modelBuilder.Entity<CareGroupLeader>()
                .HasOne(cgl => cgl.CareGroup).WithMany(cg => cg.Leaders)
                .HasForeignKey(cgl => cgl.CareGroupId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CareGroupLeader>()
                .HasOne(cgl => cgl.Member).WithMany()
                .HasForeignKey(cgl => cgl.MemberId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CareGroupLeader>()
                .HasIndex(cgl => new { cgl.CareGroupId, cgl.MemberId }).IsUnique();

            modelBuilder.Entity<CareGroupMember>()
                .HasOne(cgm => cgm.CareGroup).WithMany(cg => cg.Members)
                .HasForeignKey(cgm => cgm.CareGroupId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CareGroupMember>()
                .HasOne(cgm => cgm.Member).WithMany()
                .HasForeignKey(cgm => cgm.MemberId).OnDelete(DeleteBehavior.Cascade);
            // One care group per member, unlike Groups/Ministries — a
            // unique index on MemberId alone (not the (CareGroupId,
            // MemberId) pair MemberGroup uses), so a member can never end
            // up under two care groups' pastoral care at once.
            modelBuilder.Entity<CareGroupMember>()
                .HasIndex(cgm => cgm.MemberId).IsUnique();
        }
    }
}

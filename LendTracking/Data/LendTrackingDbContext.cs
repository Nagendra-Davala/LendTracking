using LendTracking.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace LendTracking.Data
{
    public class LendTrackingDbContext : DbContext
    {
        public LendTrackingDbContext(DbContextOptions<LendTrackingDbContext> options) : base(options) { }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<LendingRecord> LendingRecords => Set<LendingRecord>();
        public DbSet<ExpenseGroup> Groups => Set<ExpenseGroup>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
        public DbSet<GroupExpense> Expenses => Set<GroupExpense>();
        public DbSet<ExpenseShare> ExpenseShares => Set<ExpenseShare>();
        public DbSet<Settlement> Settlements => Set<Settlement>();
        public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<AppUser>(e =>
            {
                e.HasKey(x => x.UserId);
                // Case-insensitive uniqueness so "John" and "john" cannot both register.
                e.HasIndex(x => x.UserName).IsUnique();
                // Postgres treats NULLs as distinct, so optional emails stay allowed.
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.UserName).IsRequired();
                e.Property(x => x.DisplayName).IsRequired();
                e.Property(x => x.Email).IsRequired();
                e.Property(x => x.PasswordHash).IsRequired(false);
                e.HasIndex(x => x.GoogleSubjectId).IsUnique();
            });

            b.Entity<PendingRegistration>(e =>
            {
                e.HasKey(x => x.PendingRegistrationId);
                e.HasIndex(x => x.Email).IsUnique();
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasIndex(x => x.ExpiresAt);
                e.Property(x => x.Email).IsRequired();
                e.Property(x => x.PasswordHash).IsRequired();
                e.Property(x => x.TokenHash).IsRequired();
            });

            b.Entity<LendingRecord>(e =>
            {
                e.HasKey(x => x.LendingRecordId);
                e.Property(x => x.Principal).HasPrecision(18, 2);
                e.Property(x => x.AnnualRatePercent).HasPrecision(9, 4);
                e.HasOne(x => x.Owner)
                 .WithMany(u => u.LendingRecords)
                 .HasForeignKey(x => x.OwnerUserId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CounterpartyUser)
                 .WithMany()
                 .HasForeignKey(x => x.CounterpartyUserId)
                 .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.OwnerUserId);
                e.HasIndex(x => x.GivenDate);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_lending_direction", "direction IN ('Lent', 'Borrowed')");
                    t.HasCheckConstraint("ck_lending_principal_positive", "principal > 0");
                    t.HasCheckConstraint("ck_lending_rate_not_negative", "annual_rate_percent >= 0");
                    t.HasCheckConstraint("ck_lending_cleared_date", "cleared_date IS NULL OR cleared_date >= given_date");
                    t.HasCheckConstraint("ck_lending_cleared_flag", "(is_cleared AND cleared_date IS NOT NULL) OR (NOT is_cleared AND cleared_date IS NULL)");
                });
            });

            b.Entity<ExpenseGroup>(e =>
            {
                e.HasKey(x => x.GroupId);
                e.HasOne(x => x.CreatedBy)
                 .WithMany()
                 .HasForeignKey(x => x.CreatedByUserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<GroupMember>(e =>
            {
                e.HasKey(x => x.GroupMemberId);
                e.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
                e.HasOne(x => x.Group)
                 .WithMany(g => g.Members)
                 .HasForeignKey(x => x.GroupId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User)
                 .WithMany(u => u.GroupMemberships)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<GroupExpense>(e =>
            {
                e.HasKey(x => x.ExpenseId);
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.HasOne(x => x.Group)
                 .WithMany(g => g.Expenses)
                 .HasForeignKey(x => x.GroupId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PaidBy)
                 .WithMany()
                 .HasForeignKey(x => x.PaidByUserId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.GroupId);
                e.HasIndex(x => x.ExpenseDate);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_expense_split_type", "split_type IN ('equal', 'custom', 'percentage')");
                    t.HasCheckConstraint("ck_expense_amount_positive", "amount > 0");
                });
            });

            b.Entity<ExpenseShare>(e =>
            {
                e.HasKey(x => x.ExpenseShareId);
                e.Property(x => x.ShareAmount).HasPrecision(18, 2);
                e.Property(x => x.SharePercent).HasPrecision(7, 4);
                e.HasIndex(x => new { x.ExpenseId, x.UserId }).IsUnique();
                e.HasOne(x => x.Expense)
                 .WithMany(x => x.Shares)
                 .HasForeignKey(x => x.ExpenseId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_share_amount_not_negative", "share_amount >= 0");
                    t.HasCheckConstraint("ck_share_percent_range", "share_percent IS NULL OR (share_percent >= 0 AND share_percent <= 100)");
                });
            });

            b.Entity<Settlement>(e =>
            {
                e.HasKey(x => x.SettlementId);
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.HasOne(x => x.Group)
                 .WithMany(g => g.Settlements)
                 .HasForeignKey(x => x.GroupId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PaidBy)
                 .WithMany()
                 .HasForeignKey(x => x.PaidByUserId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.PaidTo)
                 .WithMany()
                 .HasForeignKey(x => x.PaidToUserId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.SettledDate);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_settlement_distinct_users", "paid_by_user_id <> paid_to_user_id");
                    t.HasCheckConstraint("ck_settlement_amount_positive", "amount > 0");
                });
            });
        }
    }
}

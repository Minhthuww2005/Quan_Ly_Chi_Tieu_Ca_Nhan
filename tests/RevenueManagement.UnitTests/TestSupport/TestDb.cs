using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Infrastructure.Persistence;

namespace RevenueManagement.UnitTests.TestSupport;

/// <summary>
/// AppDbContext trên EF Core InMemory cho test nghiệp vụ.
/// Lưu ý: InMemory không ép unique index và bỏ qua transaction, nên ở đây chỉ kiểm
/// tra lớp chống trùng bằng RunLog; unique index thật cần test trên PostgreSQL.
/// </summary>
public sealed class TestDb
{
    public const long OwnerId = 1;
    public const long OutsiderId = 2;
    public const long GroupId = 10;
    public const long OtherGroupId = 20;
    public const long WalletId = 100;
    public const long OtherWalletSameGroupId = 101;
    public const long OtherGroupWalletId = 200;
    public const long CategoryId = 1000;
    public const long OtherWalletCategoryId = 1001;

    private readonly string _name = Guid.NewGuid().ToString();

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    public static TestDb CreateSeeded()
    {
        var testDb = new TestDb();
        using var db = testDb.CreateContext();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Users.AddRange(
            new User { Id = OwnerId, Email = "owner@test.local", NormalizedEmail = "OWNER@TEST.LOCAL", DisplayName = "Owner", PasswordHash = "x", IsActive = true, CreatedAt = now, RowVersion = 1 },
            new User { Id = OutsiderId, Email = "outsider@test.local", NormalizedEmail = "OUTSIDER@TEST.LOCAL", DisplayName = "Outsider", PasswordHash = "x", IsActive = true, CreatedAt = now, RowVersion = 1 });

        db.Groups.AddRange(
            new Group { Id = GroupId, Name = "Gia đình", CreatedByUserId = OwnerId, CreatedAt = now, RowVersion = 1 },
            new Group { Id = OtherGroupId, Name = "Nhóm khác", CreatedByUserId = OutsiderId, CreatedAt = now, RowVersion = 1 });

        db.GroupMembers.AddRange(
            new GroupMember { Id = 1, GroupId = GroupId, UserId = OwnerId, Role = 1, JoinedAt = now },
            new GroupMember { Id = 2, GroupId = OtherGroupId, UserId = OutsiderId, Role = 1, JoinedAt = now });

        db.Wallets.AddRange(
            new Wallet { Id = WalletId, GroupId = GroupId, Name = "Ví chính", CreatedAt = now, RowVersion = 1 },
            new Wallet { Id = OtherWalletSameGroupId, GroupId = GroupId, Name = "Ví phụ", CreatedAt = now, RowVersion = 1 },
            new Wallet { Id = OtherGroupWalletId, GroupId = OtherGroupId, Name = "Ví nhóm khác", CreatedAt = now, RowVersion = 1 });

        db.Categories.AddRange(
            new Category { Id = CategoryId, WalletId = WalletId, Name = "Lương", Kind = TransactionKind.Income },
            new Category { Id = OtherWalletCategoryId, WalletId = OtherWalletSameGroupId, Name = "Thưởng", Kind = TransactionKind.Income });

        db.SaveChanges();
        return testDb;
    }

    public RecurringRule AddRule(Action<RecurringRule>? configure = null)
    {
        using var db = CreateContext();
        var rule = new RecurringRule
        {
            WalletId = WalletId,
            CategoryId = CategoryId,
            CreatedByUserId = OwnerId,
            Amount = 1_500_000m,
            Kind = TransactionKind.Income,
            Note = "Tiền thuê nhà",
            Frequency = RecurrenceFrequency.Daily,
            IntervalValue = 1,
            TimeZoneId = "Asia/Ho_Chi_Minh",
            StartDate = new DateOnly(2026, 10, 1),
            NextRunOn = new DateOnly(2026, 10, 7),
            IsActive = true,
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            RowVersion = 1
        };
        configure?.Invoke(rule);

        // IsActive có HasDefaultValue(true): EF coi false (giá trị CLR mặc định) là "chưa đặt"
        // khi INSERT, nên thêm ở trạng thái bật rồi mới cập nhật về false.
        var isActive = rule.IsActive;
        rule.IsActive = true;
        db.RecurringRules.Add(rule);
        db.SaveChanges();

        if (!isActive)
        {
            rule.IsActive = false;
            db.SaveChanges();
        }

        return rule;
    }
}

public sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

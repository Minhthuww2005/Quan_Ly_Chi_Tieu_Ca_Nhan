using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Infrastructure.Persistence;

public static class DemoSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync()) return;
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var now = DateTime.UtcNow;
        var users = Enumerable.Range(1, 3).Select(i => new User { Email = $"demo{i}@revenueflow.local",
            NormalizedEmail = $"DEMO{i}@REVENUEFLOW.LOCAL", DisplayName = $"Thành viên demo {i}", IsActive = true, CreatedAt = now, RowVersion = 1 }).ToArray();
        foreach (var user in users) user.PasswordHash = hasher.HashPassword(user, "Demo@123456");
        db.Users.AddRange(users); await db.SaveChangesAsync();
        for (var i = 0; i < 2; i++)
        {
            var group = new Group { Name = i == 0 ? "Nhóm đồ án .NET" : "Nhóm độc lập", Type = 1, CreatedByUserId = users[i == 0 ? 0 : 2].Id, CreatedAt = now, RowVersion = 1 };
            db.Groups.Add(group); await db.SaveChangesAsync();
            var members = i == 0 ? users.Take(2) : users.Skip(2);
            db.GroupMembers.AddRange(members.Select(u => new GroupMember { GroupId = group.Id, UserId = u.Id, Role = 2, JoinedAt = now }));
            var wallet = new Wallet { GroupId = group.Id, Name = "Ví doanh thu", CreatedAt = now, RowVersion = 1 };
            db.Wallets.Add(wallet); await db.SaveChangesAsync();
            var category = new Category { WalletId = wallet.Id, Name = "Thu nhập", Kind = TransactionKind.Income };
            db.Categories.Add(category); await db.SaveChangesAsync();
            db.RevenueTransactions.Add(new RevenueTransaction { WalletId = wallet.Id, CategoryId = category.Id,
                CreatedByUserId = group.CreatedByUserId, Amount = 5000000, Kind = TransactionKind.Income,
                OccurredOn = DateOnly.FromDateTime(now), Note = "Dữ liệu minh họa", CreatedAt = now, RowVersion = 1 });
        }
        await db.SaveChangesAsync();
    }
}

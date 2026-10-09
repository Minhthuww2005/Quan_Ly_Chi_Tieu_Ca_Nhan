using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Common.Exceptions;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Infrastructure.Workers;

namespace RevenueManagement.Api.Features.Revenues;

public sealed class RevenueService(AppDbContext db, RevenueValidator validator,
    IRevenueChangedPublisher publisher, TimeProvider clock)
{
    public async Task<IReadOnlyList<RevenueResponse>> ListAsync(long userId, long groupId,
        long? walletId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        await RequireGroupAsync(userId, groupId, ct);
        if (page < 1 || pageSize is < 1 or > 100 || (from.HasValue && to.HasValue && from > to))
            throw new AppException(400, "invalid_filter", "Bộ lọc hoặc phân trang không hợp lệ.");
        var q = db.RevenueTransactions.AsNoTracking().Where(t => t.Wallet.GroupId == groupId && !t.IsDeleted && t.Kind == TransactionKind.Income);
        if (walletId is not null) q = q.Where(t => t.WalletId == walletId);
        if (from is not null) q = q.Where(t => t.OccurredOn >= from);
        if (to is not null) q = q.Where(t => t.OccurredOn <= to);
        return await q.OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new RevenueResponse(t.Id, t.WalletId, t.CategoryId, t.CreatedByUserId, t.Amount, t.OccurredOn, t.Note, t.RowVersion)).ToListAsync(ct);
    }

    public async Task RequireGroupAsync(long userId, long groupId, CancellationToken ct)
    {
        if (!await db.GroupMembers.AnyAsync(m => m.UserId == userId && m.GroupId == groupId && m.LeftAt == null && !m.Group.IsDeleted, ct))
            throw new AppException(403, "group_access_denied", "Bạn không thuộc nhóm này.");
    }

    private async Task<RevenueTransaction> FindAsync(long userId, long id, CancellationToken ct)
    {
        var item = await db.RevenueTransactions.Include(t => t.Wallet).FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted && t.Kind == TransactionKind.Income, ct)
            ?? throw new AppException(404, "revenue_not_found", "Không tìm thấy doanh thu.");
        await RequireGroupAsync(userId, item.Wallet.GroupId, ct);
        return item;
    }
    public async Task<RevenueResponse> GetAsync(long userId, long id, CancellationToken ct) => Map(await FindAsync(userId, id, ct));

    private async Task<long> ValidateAsync(long userId, RevenueInput input, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(input, userId, ct);
        if (result.WalletNotFound) throw new AppException(404, "wallet_not_found", "Không tìm thấy ví trong nhóm của bạn.");
        if (!result.IsValid) throw new AppException(400, "invalid_revenue", string.Join(" ", result.Errors.SelectMany(x => x.Value)));
        return result.GroupId!.Value;
    }
    public async Task<RevenueResponse> CreateAsync(long userId, CreateRevenueRequest request, CancellationToken ct)
    {
        var groupId = await ValidateAsync(userId, new(request.WalletId, request.CategoryId, request.Amount, request.OccurredOn, request.Note), ct);
        var item = new RevenueTransaction { WalletId = request.WalletId, CategoryId = request.CategoryId,
            Amount = request.Amount, OccurredOn = request.OccurredOn, Note = request.Note,
            CreatedByUserId = userId, Kind = TransactionKind.Income, CreatedAt = clock.GetUtcNow().UtcDateTime, RowVersion = 1 };
        db.RevenueTransactions.Add(item);
        await db.SaveChangesAsync(ct);
        await publisher.PublishAsync(groupId, ct);
        return Map(item);
    }
    public async Task<RevenueResponse> UpdateAsync(long userId, long id, UpdateRevenueRequest request, CancellationToken ct)
    {
        var item = await FindAsync(userId, id, ct);
        if (request.RowVersion != item.RowVersion) throw new AppException(409, "concurrency_conflict", "Dữ liệu đã thay đổi. Hãy tải lại.");
        var groupId = await ValidateAsync(userId, new(request.WalletId, request.CategoryId, request.Amount, request.OccurredOn, request.Note), ct);
        if (groupId != item.Wallet.GroupId) throw new AppException(400, "invalid_wallet", "Không chuyển giao dịch sang nhóm khác.");
        item.WalletId = request.WalletId; item.CategoryId = request.CategoryId; item.Amount = request.Amount;
        item.OccurredOn = request.OccurredOn; item.Note = request.Note;
        item.UpdatedAt = clock.GetUtcNow().UtcDateTime; item.RowVersion++;
        await db.SaveChangesAsync(ct);
        await publisher.PublishAsync(groupId, ct);
        return Map(item);
    }
    public async Task DeleteAsync(long userId, long id, CancellationToken ct)
    {
        var item = await FindAsync(userId, id, ct);
        item.IsDeleted = true; item.DeletedAt = clock.GetUtcNow().UtcDateTime; item.RowVersion++;
        await db.SaveChangesAsync(ct);
        await publisher.PublishAsync(item.Wallet.GroupId, ct);
    }
    private static RevenueResponse Map(RevenueTransaction t) => new(t.Id,t.WalletId,t.CategoryId,t.CreatedByUserId,t.Amount,t.OccurredOn,t.Note,t.RowVersion);
}

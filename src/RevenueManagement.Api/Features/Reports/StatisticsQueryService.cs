using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Infrastructure.Caching;

namespace RevenueManagement.Api.Features.Reports;

public sealed class StatisticsQueryService(
    ReportingDbContext database,
    IRevenueReportCache cache,
    TimeProvider clock,
    ILogger<StatisticsQueryService> logger)
{
    public async Task<RevenueReportResult> GetRevenueAsync(
        long userId,
        RevenueReportRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var isMember = await database.GroupMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.GroupId == request.GroupId &&
                member.UserId == userId &&
                member.LeftAt == null,
                cancellationToken);
        if (!isMember)
            throw new ReportAccessDeniedException();

        if (request.WalletId is { } walletId)
        {
            var walletBelongsToGroup = await database.Wallets
                .AsNoTracking()
                .AnyAsync(wallet => wallet.Id == walletId && wallet.GroupId == request.GroupId, cancellationToken);
            if (!walletBelongsToGroup)
                throw new ReportValidationException("Ví lọc không thuộc nhóm đang xem.");
        }

        var cached = await cache.GetAsync(request, cancellationToken);
        if (cached is not null)
        {
            logger.LogInformation(
                "Revenue report cache hit for GroupId {GroupId}, WalletId {WalletId}, From {From}, To {To}, GroupBy {GroupBy}",
                request.GroupId, request.WalletId, request.From, request.To, request.GroupBy);
            return new RevenueReportResult(cached, true);
        }

        logger.LogInformation(
            "Revenue report cache miss for GroupId {GroupId}, WalletId {WalletId}, From {From}, To {To}, GroupBy {GroupBy}",
            request.GroupId, request.WalletId, request.From, request.To, request.GroupBy);

        var query =
            from transaction in database.Transactions.AsNoTracking()
            join wallet in database.Wallets.AsNoTracking() on transaction.WalletId equals wallet.Id
            where wallet.GroupId == request.GroupId &&
                  transaction.Kind == TransactionKind.Income &&
                  !transaction.IsDeleted &&
                  transaction.OccurredOn >= request.From &&
                  transaction.OccurredOn <= request.To
            select transaction;

        if (request.WalletId is { } selectedWalletId)
            query = query.Where(transaction => transaction.WalletId == selectedWalletId);

        var total = await query.SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0m;
        var breakdown = await BuildBreakdownAsync(query, request.GroupBy, cancellationToken);

        var report = new RevenueReportResponse(
            request.GroupId,
            request.From,
            request.To,
            request.WalletId,
            request.GroupBy,
            total,
            breakdown,
            clock.GetUtcNow());

        await cache.SetAsync(request, report, cancellationToken);
        return new RevenueReportResult(report, false);
    }

    private async Task<IReadOnlyList<RevenueBreakdownResponse>> BuildBreakdownAsync(
        IQueryable<ReportTransaction> query,
        RevenueReportGroupBy groupBy,
        CancellationToken cancellationToken)
    {
        return groupBy switch
        {
            RevenueReportGroupBy.Date => await GroupByDateAsync(query, cancellationToken),
            RevenueReportGroupBy.Wallet => await GroupByWalletAsync(query, cancellationToken),
            RevenueReportGroupBy.Category => await GroupByCategoryAsync(query, cancellationToken),
            RevenueReportGroupBy.Member => await GroupByMemberAsync(query, cancellationToken),

            _ => throw new ReportValidationException("groupBy không hợp lệ.")
        };
    }

    private static async Task<IReadOnlyList<RevenueBreakdownResponse>> GroupByDateAsync(
        IQueryable<ReportTransaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(transaction => transaction.OccurredOn)
            .Select(group => new { Date = group.Key, Revenue = group.Sum(item => item.Amount) })
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RevenueBreakdownResponse(
            item.Date.ToString("yyyy-MM-dd"), item.Date.ToString("yyyy-MM-dd"), item.Revenue)).ToList();
    }

    private async Task<IReadOnlyList<RevenueBreakdownResponse>> GroupByWalletAsync(
        IQueryable<ReportTransaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from transaction in query
                join wallet in database.Wallets.AsNoTracking() on transaction.WalletId equals wallet.Id
                group transaction by new { wallet.Id, wallet.Name }
                into grouped
                orderby grouped.Key.Name
                select new { grouped.Key.Id, grouped.Key.Name, Revenue = grouped.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RevenueBreakdownResponse(
            item.Id.ToString(), item.Name, item.Revenue)).ToList();
    }

    private async Task<IReadOnlyList<RevenueBreakdownResponse>> GroupByCategoryAsync(
        IQueryable<ReportTransaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from transaction in query
                join category in database.Categories.AsNoTracking()
                    on transaction.CategoryId equals (long?)category.Id into matchingCategories
                from category in matchingCategories.DefaultIfEmpty()
                group transaction by new
                {
                    Id = transaction.CategoryId,
                    Name = category == null ? "Không phân loại" : category.Name
                }
                into grouped
                orderby grouped.Key.Name
                select new { grouped.Key.Id, grouped.Key.Name, Revenue = grouped.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RevenueBreakdownResponse(
            item.Id?.ToString() ?? "uncategorized", item.Name, item.Revenue)).ToList();
    }

    private static async Task<IReadOnlyList<RevenueBreakdownResponse>> GroupByMemberAsync(
        IQueryable<ReportTransaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(transaction => transaction.CreatedByUserId)
            .Select(group => new { UserId = group.Key, Revenue = group.Sum(item => item.Amount) })
            .OrderBy(item => item.UserId)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RevenueBreakdownResponse(
            item.UserId.ToString(), item.UserId.ToString(), item.Revenue)).ToList();
    }

    private static void Validate(RevenueReportRequest request)
    {
        if (request.GroupId <= 0)
            throw new ReportValidationException("groupId là bắt buộc.");
        if (request.From == default || request.To == default)
            throw new ReportValidationException("from và to là bắt buộc.");
        if (request.From > request.To)
            throw new ReportValidationException("from phải nhỏ hơn hoặc bằng to.");
        if (request.To.DayNumber - request.From.DayNumber > 366)
            throw new ReportValidationException("Khoảng báo cáo tối đa là 366 ngày.");
        if (!Enum.IsDefined(request.GroupBy))
            throw new ReportValidationException("groupBy không hợp lệ.");
    }
}

public sealed class ReportValidationException(string message) : Exception(message);
public sealed class ReportAccessDeniedException : Exception;

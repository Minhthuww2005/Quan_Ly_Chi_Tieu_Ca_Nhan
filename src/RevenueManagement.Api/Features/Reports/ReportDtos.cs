namespace RevenueManagement.Api.Features.Reports;

public enum RevenueReportGroupBy
{
    Date,
    Wallet,
    Category,
    Member
}

public sealed class RevenueReportRequest
{
    public long GroupId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public long? WalletId { get; init; }
    public RevenueReportGroupBy GroupBy { get; init; } = RevenueReportGroupBy.Date;
}

public sealed record RevenueReportResponse(
    long GroupId,
    DateOnly From,
    DateOnly To,
    long? WalletId,
    RevenueReportGroupBy GroupBy,
    decimal TotalRevenue,
    IReadOnlyList<RevenueBreakdownResponse> Breakdown,
    DateTimeOffset GeneratedAtUtc);

public sealed record RevenueBreakdownResponse(
    string Key,
    string Label,
    decimal Revenue);

public sealed record RevenueReportResult(RevenueReportResponse Report, bool CacheHit);

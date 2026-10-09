using RevenueManagement.Api.Features.Reports;
using RevenueManagement.Api.Infrastructure.Caching;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RevenueReportCacheKeyTests
{
    [Fact]
    public void CreateKey_ContainsEveryQueryDimension()
    {
        const long groupId = 101;
        const long walletId = 202;
        var request = new RevenueReportRequest
        {
            GroupId = groupId,
            WalletId = walletId,
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 1, 31),
            GroupBy = RevenueReportGroupBy.Category
        };

        var key = RedisCacheService.CreateKey(request, "version-7");

        Assert.Contains("g:101", key);
        Assert.Contains("w:202", key);
        Assert.Contains("from:20260101", key);
        Assert.Contains("to:20260131", key);
        Assert.Contains("by:category", key);
        Assert.EndsWith("v:version-7", key);
    }

    [Fact]
    public void CreateKey_ChangesWhenGroupingChanges()
    {
        var request = new RevenueReportRequest
        {
            GroupId = 101,
            From = new DateOnly(2026, 2, 1),
            To = new DateOnly(2026, 2, 28),
            GroupBy = RevenueReportGroupBy.Date
        };

        var dateKey = RedisCacheService.CreateKey(request, "1");
        var walletKey = RedisCacheService.CreateKey(
            new RevenueReportRequest
            {
                GroupId = request.GroupId,
                From = request.From,
                To = request.To,
                GroupBy = RevenueReportGroupBy.Wallet
            },
            "1");

        Assert.NotEqual(dateKey, walletKey);
    }
}

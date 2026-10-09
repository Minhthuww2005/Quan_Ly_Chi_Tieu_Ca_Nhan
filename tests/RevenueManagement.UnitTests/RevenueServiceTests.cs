using Microsoft.Extensions.Logging.Abstractions;
using RevenueManagement.Api.Common.Exceptions;
using RevenueManagement.Api.Features.Revenues;
using RevenueManagement.Api.Infrastructure.Workers;
using RevenueManagement.UnitTests.TestSupport;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RevenueServiceTests
{
    [Fact]
    public async Task CreateUpdateDelete_UsesJwtOwnerAndPublishesAfterSaving()
    {
        var data = TestDb.CreateSeeded();
        await using var db = data.CreateContext();
        var events = new RecordingPublisher();
        var service = new RevenueService(db, new(db, TimeProvider.System), events, TimeProvider.System);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await service.CreateAsync(TestDb.OwnerId, new(TestDb.WalletId, TestDb.CategoryId, 100, date, "test"), TestContext.Current.CancellationToken);
        Assert.Equal(TestDb.OwnerId, created.CreatedByUserId);
        Assert.Equal(1, created.RowVersion);
        var updated = await service.UpdateAsync(TestDb.OwnerId, created.Id, new(TestDb.WalletId, TestDb.CategoryId, 200, date, "edit", 1), TestContext.Current.CancellationToken);
        Assert.Equal(200, updated.Amount);
        var conflict = await Assert.ThrowsAsync<AppException>(() => service.UpdateAsync(TestDb.OwnerId, created.Id, new(TestDb.WalletId, TestDb.CategoryId, 300, date, "stale", 1), TestContext.Current.CancellationToken));
        Assert.Equal(409, conflict.StatusCode);
        await service.DeleteAsync(TestDb.OwnerId, created.Id, TestContext.Current.CancellationToken);
        Assert.Empty(await service.ListAsync(TestDb.OwnerId, TestDb.GroupId, null, null, null, 1, 50, TestContext.Current.CancellationToken));
        Assert.Equal(3, events.Groups.Count);
    }
    [Fact]
    public async Task OutsiderCannotReadOrChangeRevenue_AndInvalidAmountDoesNotWrite()
    {
        var data = TestDb.CreateSeeded();
        await using var db = data.CreateContext();
        var events = new RecordingPublisher();
        var service = new RevenueService(db, new(db, TimeProvider.System), events, TimeProvider.System);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var invalid = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(TestDb.OwnerId,new(TestDb.WalletId,TestDb.CategoryId,0,date,null),TestContext.Current.CancellationToken));
        Assert.Equal(400, invalid.StatusCode);
        Assert.Empty(events.Groups);
        var created = await service.CreateAsync(TestDb.OwnerId,new(TestDb.WalletId,TestDb.CategoryId,100,date,null),TestContext.Current.CancellationToken);
        var forbidden = await Assert.ThrowsAsync<AppException>(() => service.GetAsync(TestDb.OutsiderId,created.Id,TestContext.Current.CancellationToken));
        Assert.Equal(403, forbidden.StatusCode);
    }
    private sealed class RecordingPublisher : IRevenueChangedPublisher
    {
        public List<long> Groups { get; } = [];
        public Task PublishAsync(long groupId, CancellationToken ct = default) { Groups.Add(groupId); return Task.CompletedTask; }
    }
}

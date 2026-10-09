using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RevenueManagement.Api.Infrastructure.Realtime;
using RevenueManagement.Api.Infrastructure.Workers;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RealtimePublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsRevenueChanged_ToCorrectGroup()
    {
        // Arrange
        const long groupId = 42;
        var expectedGroupName = $"group-{groupId}";

        var clientProxyMock = new Mock<IRevenueHubClient>();
        var hubClientsMock = new Mock<IHubClients<IRevenueHubClient>>();
        var hubContextMock = new Mock<IHubContext<DashboardHub, IRevenueHubClient>>();

        hubClientsMock
            .Setup(clients => clients.Group(expectedGroupName))
            .Returns(clientProxyMock.Object);

        hubContextMock
            .Setup(context => context.Clients)
            .Returns(hubClientsMock.Object);

        var publisher = new SignalRRevenueChangedPublisher(hubContextMock.Object);

        // Act
        await publisher.PublishAsync(groupId, TestContext.Current.CancellationToken);

        // Assert
        hubClientsMock.Verify(
            clients => clients.Group(expectedGroupName),
            Times.Once);

        clientProxyMock.Verify(
            client => client.RevenueChanged(
                groupId,
                "Dữ liệu doanh thu đã được cập nhật",
                It.Is<DateTime>(dt => (DateTime.UtcNow - dt).TotalSeconds < 5)),
            Times.Once);
    }

    [Fact]
    public async Task JoinGroup_AddsConnectionIdToGroup()
    {
        // Arrange
        const long groupId = 100;
        const string connectionId = "conn-abc-123";

        var groupsMock = new Mock<IGroupManager>();
        var callerContextMock = new Mock<HubCallerContext>();

        callerContextMock
            .Setup(c => c.ConnectionId)
            .Returns(connectionId);

        var claims = new[] { new Claim("sub", "user-555") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        callerContextMock
            .Setup(c => c.User)
            .Returns(principal);

        var hub = new DashboardHub
        {
            Groups = groupsMock.Object,
            Context = callerContextMock.Object
        };

        // Act
        await hub.JoinGroup(groupId);

        // Assert
        groupsMock.Verify(
            g => g.AddToGroupAsync(connectionId, $"group-{groupId}", default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveGroup_RemovesConnectionIdFromGroup()
    {
        // Arrange
        const long groupId = 100;
        const string connectionId = "conn-abc-123";

        var groupsMock = new Mock<IGroupManager>();
        var callerContextMock = new Mock<HubCallerContext>();

        callerContextMock
            .Setup(c => c.ConnectionId)
            .Returns(connectionId);

        var hub = new DashboardHub
        {
            Groups = groupsMock.Object,
            Context = callerContextMock.Object
        };

        // Act
        await hub.LeaveGroup(groupId);

        // Assert
        groupsMock.Verify(
            g => g.RemoveFromGroupAsync(connectionId, $"group-{groupId}", default),
            Times.Once);
    }

    [Fact]
    public void AddRealtimeServices_RegistersSignalRAndPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddRealtimeServices();
        var provider = services.BuildServiceProvider();

        // Assert
        var publisher = provider.GetService<IRevenueChangedPublisher>();
        Assert.NotNull(publisher);
        Assert.IsType<SignalRRevenueChangedPublisher>(publisher);
    }
}

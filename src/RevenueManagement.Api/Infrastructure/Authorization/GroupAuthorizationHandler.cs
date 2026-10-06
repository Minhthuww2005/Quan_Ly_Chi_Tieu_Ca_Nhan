using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Infrastructure.Persistence;

namespace RevenueManagement.Api.Infrastructure.Authorization;

public sealed class GroupAuthorizationHandler
    : AuthorizationHandler<GroupMemberRequirement, long>
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;

    public GroupAuthorizationHandler(
        AppDbContext db,
        CurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupMemberRequirement requirement,
        long groupId)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return;
        }

        var isMember = await _db.GroupMembers
            .AsNoTracking()
            .AnyAsync(x =>
                x.GroupId == groupId &&
                x.UserId == userId.Value &&
                x.LeftAt == null);

        if (isMember)
        {
            context.Succeed(requirement);
        }
    }
}

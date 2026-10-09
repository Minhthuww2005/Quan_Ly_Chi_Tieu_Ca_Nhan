namespace RevenueManagement.Api.Features.Revenues;
public sealed record CreateRevenueRequest(long WalletId, long? CategoryId, decimal Amount, DateOnly OccurredOn, string? Note);
public sealed record UpdateRevenueRequest(long WalletId, long? CategoryId, decimal Amount, DateOnly OccurredOn, string? Note, long RowVersion);
public sealed record RevenueResponse(long Id, long WalletId, long? CategoryId, long CreatedByUserId,
    decimal Amount, DateOnly OccurredOn, string? Note, long RowVersion);

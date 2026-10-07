namespace RevenueManagement.Api.Domain.Enums;
/// <summary>
/// Khớp CHECK constraint transactions/categories/recurring_rules.kind IN (1, 2) trên database.
/// 2 = Expense đã có trong schema nhưng nằm ngoài phạm vi đồ án (SCOPE.md).
/// </summary>
public enum TransactionKind { Income = 1 }


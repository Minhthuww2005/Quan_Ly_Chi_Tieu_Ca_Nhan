using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Infrastructure.Persistence;

namespace RevenueManagement.Api.Features.Revenues;

/// <summary>Dữ liệu cần kiểm tra của một khoản doanh thu (dùng chung cho REV-01 và REV-03).</summary>
public sealed record RevenueInput(
    long WalletId,
    long? CategoryId,
    decimal Amount,
    DateOnly OccurredOn,
    string? Note);

/// <summary>Kết quả kiểm tra tham chiếu Wallet/Category.</summary>
public sealed class RevenueValidationResult
{
    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>
    /// true khi Wallet không tồn tại hoặc user không phải thành viên của Group sở hữu Wallet.
    /// Hai trường hợp gộp chung thành 404 để không lộ dữ liệu nhóm khác (chống IDOR).
    /// </summary>
    public bool WalletNotFound { get; private set; }

    public long? GroupId { get; internal set; }

    public bool IsValid => !WalletNotFound && _errors.Count == 0;

    public IReadOnlyDictionary<string, string[]> Errors
        => _errors.ToDictionary(x => x.Key, x => x.Value.ToArray());

    internal void MarkWalletNotFound() => WalletNotFound = true;

    internal void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = [];
        list.Add(message);
    }

    internal void AddRange(string field, IEnumerable<string> messages)
    {
        foreach (var message in messages) Add(field, message);
    }
}

/// <summary>
/// 1B.3 — Validation nghiệp vụ cho doanh thu:
/// Amount &gt; 0 (tối đa 4 chữ số thập phân, vừa numeric(19,4)), ngày hợp lệ,
/// Wallet và Category cùng Group (Category thuộc đúng Wallet), Category là Income,
/// user phải là thành viên đang hoạt động của Group.
///
/// Lớp này không phụ thuộc HTTP: Minimal API /api/revenues (A) và RecurringRuleService (C)
/// cùng gọi để không lặp logic.
/// </summary>
public sealed class RevenueValidator(AppDbContext db, TimeProvider clock)
{
    public const int NoteMaxLength = 500;

    /// <summary>numeric(19,4): tối đa 15 chữ số phần nguyên.</summary>
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    public static readonly DateOnly MinOccurredOn = new(2000, 1, 1);

    /// <summary>Cho phép lệch 1 ngày vì client và server có thể khác múi giờ.</summary>
    public const int MaxDaysInFuture = 1;

    public static IEnumerable<string> ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            yield return "Amount phải lớn hơn 0.";
        else if (amount > MaxAmount)
            yield return "Amount vượt quá giới hạn cho phép.";

        if (amount.Scale > 4 && decimal.Round(amount, 4) != amount)
            yield return "Amount chỉ được có tối đa 4 chữ số thập phân.";
    }

    public static IEnumerable<string> ValidateOccurredOn(DateOnly occurredOn, DateOnly today)
    {
        if (occurredOn < MinOccurredOn)
            yield return $"Ngày phát sinh không được trước {MinOccurredOn:yyyy-MM-dd}.";
        else if (occurredOn > today.AddDays(MaxDaysInFuture))
            yield return "Ngày phát sinh không được ở tương lai.";
    }

    public static IEnumerable<string> ValidateNote(string? note)
    {
        if (note is not null && note.Length > NoteMaxLength)
            yield return $"Note tối đa {NoteMaxLength} ký tự.";
    }

    /// <summary>Kiểm tra đầy đủ một khoản doanh thu nhập tay (REV-01).</summary>
    public async Task<RevenueValidationResult> ValidateAsync(
        RevenueInput input,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateReferencesAsync(userId, input.WalletId, input.CategoryId, cancellationToken);
        if (result.WalletNotFound) return result;

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        result.AddRange(nameof(RevenueInput.Amount), ValidateAmount(input.Amount));
        result.AddRange(nameof(RevenueInput.OccurredOn), ValidateOccurredOn(input.OccurredOn, today));
        result.AddRange(nameof(RevenueInput.Note), ValidateNote(input.Note));
        return result;
    }

    /// <summary>
    /// Kiểm tra Wallet thuộc Group mà user là thành viên, Wallet chưa lưu trữ,
    /// Category (nếu có) thuộc đúng Wallet, chưa lưu trữ và có Kind = Income.
    /// </summary>
    public async Task<RevenueValidationResult> ValidateReferencesAsync(
        long userId,
        long walletId,
        long? categoryId,
        CancellationToken cancellationToken = default)
    {
        var result = new RevenueValidationResult();

        var wallet = await db.Wallets
            .AsNoTracking()
            .Where(w => w.Id == walletId
                && !w.Group.IsDeleted
                && w.Group.Members.Any(m => m.UserId == userId && m.LeftAt == null))
            .Select(w => new { w.Id, w.GroupId, w.IsArchived })
            .FirstOrDefaultAsync(cancellationToken);

        if (wallet is null)
        {
            result.MarkWalletNotFound();
            return result;
        }

        result.GroupId = wallet.GroupId;

        if (wallet.IsArchived)
            result.Add("WalletId", "Ví đã được lưu trữ, không thể ghi doanh thu.");

        if (categoryId is { } id)
        {
            var category = await db.Categories
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new { c.WalletId, GroupId = c.Wallet.GroupId, c.Kind, c.IsArchived })
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null || category.GroupId != wallet.GroupId)
                result.Add("CategoryId", "Danh mục không tồn tại trong nhóm của ví.");
            else if (category.WalletId != wallet.Id)
                result.Add("CategoryId", "Danh mục không thuộc ví đã chọn.");
            else if (category.Kind != TransactionKind.Income)
                result.Add("CategoryId", "Danh mục phải là loại Income.");
            else if (category.IsArchived)
                result.Add("CategoryId", "Danh mục đã được lưu trữ.");
        }

        return result;
    }
}

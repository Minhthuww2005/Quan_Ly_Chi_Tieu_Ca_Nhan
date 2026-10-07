namespace RevenueManagement.Api.Features.Recurring;

/// <summary>
/// Giá trị cột recurring_run_logs.status (smallint).
/// Đánh số từ 1 cho khớp quy ước của database (kind: 1 = Income).
/// </summary>
public enum RecurringRunStatus : short
{
    /// <summary>Đã tạo giao dịch thành công.</summary>
    Created = 1,

    /// <summary>Kỳ chạy bị bỏ qua (ví/danh mục đã lưu trữ, người tạo rời nhóm...).</summary>
    Skipped = 2,

    /// <summary>Kỳ chạy thất bại sau khi đã retry hết số lần cho phép.</summary>
    Failed = 3
}

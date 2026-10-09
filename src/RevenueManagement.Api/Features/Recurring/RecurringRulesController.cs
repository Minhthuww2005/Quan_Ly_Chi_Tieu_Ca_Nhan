using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevenueManagement.Api.Common.Models;
using RevenueManagement.Api.Infrastructure.Authorization;

namespace RevenueManagement.Api.Features.Recurring;

/// <summary>
/// REV-03 — Quản lý quy tắc doanh thu định kỳ (Daily/Weekly/Monthly là tham số Frequency).
/// [ApiController] tự trả 400 khi binding/DataAnnotations/IValidatableObject lỗi.
/// Controller mỏng: chỉ map kết quả service sang HTTP status.
/// </summary>
[ApiController]
[Authorize]
[Route("api/recurring-rules")]
[Produces("application/json")]
public sealed class RecurringRulesController(RecurringRuleService service, CurrentUser currentUser) : ControllerBase
{
    /// <summary>Danh sách quy tắc trong các nhóm user là thành viên; lọc theo ví nếu có walletId.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecurringRuleResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] long? walletId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await service.ListAsync(userId, walletId, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<RecurringRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return ToActionResult(await service.GetAsync(userId, id, cancellationToken), value => Ok(value));
    }

    [HttpPost]
    [ProducesResponseType<RecurringRuleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateRecurringRuleRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.CreateAsync(userId, request, cancellationToken);
        return ToActionResult(result, rule => CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule));
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<RecurringRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRecurringRuleRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return ToActionResult(await service.UpdateAsync(userId, id, request, cancellationToken), value => Ok(value));
    }

    /// <summary>Bật/tắt quy tắc. Bật lại thì chạy tiếp từ hôm nay, không bù các kỳ đã bỏ lỡ.</summary>
    [HttpPatch("{id:long}/active")]
    [ProducesResponseType<RecurringRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetActive(long id, [FromBody] SetRecurringRuleActiveRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return ToActionResult(await service.SetActiveAsync(userId, id, request, cancellationToken), value => Ok(value));
    }

    /// <summary>Chỉ xóa được quy tắc chưa chạy lần nào; quy tắc đã có nhật ký thì tắt thay vì xóa.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return ToActionResult(await service.DeleteAsync(userId, id, cancellationToken), _ => NoContent());
    }

    /// <summary>Nhật ký chạy (Created/Skipped/Failed), mới nhất trước.</summary>
    [HttpGet("{id:long}/runs")]
    [ProducesResponseType<IReadOnlyList<RecurringRunLogResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRuns(long id, [FromQuery] RecurringRunLogQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return ToActionResult(await service.GetRunsAsync(userId, id, query, cancellationToken), value => Ok(value));
    }

    /// <summary>Đưa quy tắc vào hàng đợi ngay (202). Kỳ chưa đến hạn sẽ không được tạo.</summary>
    [HttpPost("{id:long}/trigger")]
    [ProducesResponseType<RecurringTriggerResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Trigger(long id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return ToActionResult(await service.TriggerAsync(userId, id, cancellationToken), value => Accepted(value));
    }

    private IActionResult ToActionResult<T>(RecurringResult<T> result, Func<T, IActionResult> onOk)
        => result.Status switch
        {
            RecurringResultStatus.Ok => onOk(result.Value!),
            RecurringResultStatus.NotFound => NotFound(Error(StatusCodes.Status404NotFound, "Không tìm thấy quy tắc định kỳ hoặc ví.")),
            RecurringResultStatus.Conflict => Conflict(Error(StatusCodes.Status409Conflict, result.Message ?? "Xung đột dữ liệu.")),
            RecurringResultStatus.Invalid => ValidationProblem(new ValidationProblemDetails(
                result.Errors!.ToDictionary(x => x.Key, x => x.Value))),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };

    private ErrorResponse Error(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        Message = message,
        RequestId = HttpContext.TraceIdentifier,
        Timestamp = DateTime.UtcNow
    };
}

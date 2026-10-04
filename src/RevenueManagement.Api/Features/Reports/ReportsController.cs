using Microsoft.AspNetCore.Mvc;

namespace RevenueManagement.Api.Features.Reports;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(
    StatisticsQueryService statistics,
    IReportCurrentUser currentUser) : ControllerBase
{
    [HttpGet("revenue")]
    [ProducesResponseType<RevenueReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RevenueReportResponse>> GetRevenue(
        [FromQuery] RevenueReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetUserId(out var userId))
            return Unauthorized(new { code = "authentication_required", message = "Cần đăng nhập để xem báo cáo." });

        try
        {
            var result = await statistics.GetRevenueAsync(userId, request, cancellationToken);
            Response.Headers["X-Report-Cache"] = result.CacheHit ? "HIT" : "MISS";
            return Ok(result.Report);
        }
        catch (ReportValidationException exception)
        {
            return BadRequest(new { code = "invalid_report_filter", message = exception.Message });
        }
        catch (ReportAccessDeniedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { code = "report_access_denied", message = "Bạn không có quyền xem báo cáo của nhóm này." });
        }
    }
}

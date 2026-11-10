using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
[Route("api/admin/schedule-change-policy")]
public sealed class ScheduleChangePolicyController : ControllerBase
{
    private readonly IScheduleChangePolicyService _service;
    public ScheduleChangePolicyController(IScheduleChangePolicyService service) => _service = service;

    [HttpGet("active")]
    public async Task<IActionResult> Active()
    {
        var policy = await _service.GetActiveAsync(RiversideBusinessClock.Now, HttpContext.RequestAborted);
        return Ok(new { success = true, data = new
        {
            policy.PolicyId, policy.PolicyCode, policy.Version, policy.OnlineMinimumDays,
            policy.NewDateMinimumDays, policy.PaymentWindowMinutes, policy.HoldWindowMinutes,
            policy.EffectiveFrom, policy.EffectiveTo, policy.Status,
            tiers = policy.Tiers.OrderBy(x => x.MinimumDays).Select(x => new
            { x.MinimumDays, x.MaximumDays, x.FeeAmount, x.CustomerOnlineAllowed })
        }});
    }

    [HttpGet("history")]
    public async Task<IActionResult> History() => Ok(new { success = true,
        data = await _service.GetHistoryAsync(HttpContext.RequestAborted) });

    [HttpPost("versions")]
    public async Task<IActionResult> CreateVersion([FromBody] ScheduleChangePolicyVersionRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return StatusCode(StatusCodes.Status201Created, new { success = true,
                data = await _service.CreateVersionAsync(userId, request, HttpContext.RequestAborted) });
        }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }
}

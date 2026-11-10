using System.Text.Json;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Coordinator + "," + RoleNames.HallManager)]
[Route("dieu-phoi/checklist")]
public sealed class ChecklistController(IRiversideApiClient api) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("data")]
    public async Task<IActionResult> Data(CancellationToken ct)
    {
        var token = Request.Cookies["rp_api_token"];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized(new { message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
        var result = await api.GetChecklistsAsync(token, ct);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost("{bookingId:int}/{operation}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Execute(int bookingId, string operation, [FromBody] JsonElement command, CancellationToken ct)
    {
        if (command.ValueKind != JsonValueKind.Object) return BadRequest(new { message = "Dữ liệu công việc không hợp lệ." });
        var token = Request.Cookies["rp_api_token"];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized(new { message = "Phiên đăng nhập hết hạn." });
        var result = await api.ExecuteChecklistAsync(token, bookingId, operation, command, ct);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }
}

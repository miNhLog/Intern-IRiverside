using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.ManagerHr;
using HeThongDatTiecCuoi_WEB.Models.RoleChangeRequest;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles=RoleNames.Manager)]
[Route("quan-ly/nhan-su")]
public sealed class ManagerHrController : Controller
{
    private const string TokenCookie="rp_api_token"; private readonly IRiversideApiClient _api;
    public ManagerHrController(IRiversideApiClient api)=>_api=api;
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search,string? role,string? status,string? assignmentStatus,int page=1,CancellationToken cancellationToken=default)
    {
        var token=Request.Cookies[TokenCookie]; if(string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login","Auth");
        var result=await _api.GetManagerHrAsync(token,search,role,status,assignmentStatus,page,10,cancellationToken);
        var model=result.Value??new ManagerHrPageViewModel{ErrorMessage=result.Error??"Không thể tải dữ liệu nhân sự."}; model.Search=search;model.Role=role;model.Status=status;model.AssignmentStatus=assignmentStatus; return View(model);
    }
    [HttpPost("yeu-cau-vai-tro")][ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoleRequest([FromForm]CreateRoleChangeRequestViewModel model,CancellationToken cancellationToken)
    { var token=Request.Cookies[TokenCookie]; if(string.IsNullOrWhiteSpace(token)) return Unauthorized(); if(!ModelState.IsValid) return BadRequest(new{message=ModelState.Values.SelectMany(v=>v.Errors).FirstOrDefault()?.ErrorMessage}); var r=await _api.CreateRoleChangeRequestAsync(model,token,cancellationToken); return r.Succeeded?Ok(new{message="Đã gửi yêu cầu thay đổi vai trò."}):StatusCode(r.StatusCode??400,new{message=r.Error}); }
    [HttpPost("bo-nhiem/{employeeId:int}")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int employeeId,[FromForm]AssignHallsViewModel model,CancellationToken cancellationToken)
    { var token=Request.Cookies[TokenCookie]; if(string.IsNullOrWhiteSpace(token)) return Unauthorized(); var r=await _api.AssignManagerHallsAsync(employeeId,model,token,cancellationToken); return r.Succeeded?Ok(r.Value):StatusCode(r.StatusCode??400,new{message=r.Error}); }
    [HttpPost("ket-thuc/{assignmentId:int}")][ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int assignmentId,CancellationToken cancellationToken)
    { var token=Request.Cookies[TokenCookie]; if(string.IsNullOrWhiteSpace(token)) return Unauthorized(); var r=await _api.EndManagerHallAssignmentAsync(assignmentId,token,cancellationToken); return r.Succeeded?Ok(r.Value):StatusCode(r.StatusCode??400,new{message=r.Error}); }
}

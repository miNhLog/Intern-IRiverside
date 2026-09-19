using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.DTOs.Policies;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/policies")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class PoliciesController : ControllerBase
{
    private readonly IPolicyStore _policyStore;

    public PoliciesController(IPolicyStore policyStore)
    {
        _policyStore = policyStore;
    }

    [HttpGet]
    [ProducesResponseType<PolicyResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _policyStore.GetAsync(cancellationToken));
        }
        catch (PolicyStoreException exception)
        {
            return ToError(exception);
        }
    }

    [HttpPut]
    [ProducesResponseType<PolicyResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(
        UpdatePolicyRequestDto request,
        CancellationToken cancellationToken)
    {
        var errors = PolicyValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new ApiErrorResponse("The request is invalid.", errors));
        }

        try
        {
            var response = await _policyStore.UpdateAsync(
                request.ExpectedVersion!.Value,
                PolicyValidator.Normalize(request),
                cancellationToken);

            return Ok(response);
        }
        catch (PolicyStoreException exception)
        {
            return ToError(exception);
        }
    }

    private ObjectResult ToError(PolicyStoreException exception) =>
        StatusCode(
            exception.StatusCode,
            new ApiErrorResponse(exception.Message));
}

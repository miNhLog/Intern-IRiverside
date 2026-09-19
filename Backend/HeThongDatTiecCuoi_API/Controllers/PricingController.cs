using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.DTOs.Pricing;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/pricing")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class PricingController : ControllerBase
{
    private readonly IPricingService _pricingService;

    public PricingController(IPricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpGet]
    [ProducesResponseType<PricingResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _pricingService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{category}")]
    [ProducesResponseType<PricingCategoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategory(
        string category,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedCategory(category))
        {
            return InvalidCategory();
        }

        return Ok(await _pricingService.GetCategoryAsync(category, cancellationToken));
    }

    [HttpPut("{category}/{id:int}")]
    [ProducesResponseType<PricingItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePrice(
        string category,
        int id,
        UpdatePriceRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedCategory(category))
        {
            return InvalidCategory();
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Price.HasValue && !PriceValidation.IsValid(request.Price.Value))
        {
            errors["price"] = ["Price must have no more than two decimal places and be within the allowed range."];
        }

        if (request.ExpectedPrice.HasValue && !PriceValidation.IsValid(request.ExpectedPrice.Value))
        {
            errors["expectedPrice"] = ["Expected price must have no more than two decimal places and be within the allowed range."];
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ApiErrorResponse("The request is invalid.", errors));
        }

        try
        {
            var item = await _pricingService.UpdatePriceAsync(
                category,
                id,
                request.Price!.Value,
                request.ExpectedPrice!.Value,
                cancellationToken);

            return Ok(item);
        }
        catch (PricingServiceException exception)
        {
            return StatusCode(
                exception.StatusCode,
                new ApiErrorResponse(exception.Message));
        }
    }

    private static bool IsSupportedCategory(string category) =>
        PricingCategoryCatalog.All.Contains(
            PricingCategoryCatalog.Normalize(category),
            StringComparer.Ordinal);

    private BadRequestObjectResult InvalidCategory() =>
        BadRequest(new ApiErrorResponse(
            "The pricing category is not supported.",
            new Dictionary<string, string[]>
            {
                ["category"] = ["Category must be halls, menus, decor-packages, or services."]
            }));
}

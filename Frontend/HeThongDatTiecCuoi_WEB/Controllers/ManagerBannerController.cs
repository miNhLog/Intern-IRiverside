using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Banners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerBannerController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IHttpClientFactory _clients;
    public ManagerBannerController(IHttpClientFactory clients) => _clients = clients;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await Send<List<BannerViewModel>>(HttpMethod.Get, "api/banners", null, ct);
        var banners = result.Value ?? [];
        foreach (var banner in banners) banner.ImageUrl = ImageUrl(banner.ImagePath, banner.UpdatedAt);
        if (!result.Ok) TempData["ErrorMessage"] = result.Error;
        return View(new BannerManagementViewModel { Banners = banners });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BannerFormViewModel model, CancellationToken ct)
    {
        var isNew = !model.BannerId.HasValue || model.BannerId.Value <= 0;
        if (!ModelState.IsValid || (isNew && model.Image is null))
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m));
            var message = string.Join("; ", errors);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = isNew && model.Image is null ? "Vui lòng chọn ảnh cho banner mới." : "Dữ liệu banner chưa hợp lệ.";
            }
            TempData["ErrorMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        var primaryAction = string.IsNullOrWhiteSpace(model.PrimaryActionType) ? "NONE" : model.PrimaryActionType.Trim().ToUpperInvariant();
        var secondaryAction = string.IsNullOrWhiteSpace(model.SecondaryActionType) ? "NONE" : model.SecondaryActionType.Trim().ToUpperInvariant();

        var payload = new {
            Placement = model.Placement.Trim().ToUpperInvariant(),
            Title = model.Title.Trim(),
            Subtitle = model.Subtitle.Trim(),
            Description = model.Description.Trim(),
            NavigationLabel = model.NavigationLabel.Trim(),
            ImageAlt = model.ImageAlt.Trim(),
            PrimaryActionType = primaryAction,
            PrimaryButtonLabel = model.PrimaryButtonLabel,
            PrimaryLink = model.PrimaryLink,
            SecondaryActionType = secondaryAction,
            SecondaryButtonLabel = model.SecondaryButtonLabel,
            SecondaryLink = model.SecondaryLink,
            model.SortOrder,
            model.IsVisible,
            model.Version
        };

        var method = isNew ? HttpMethod.Post : HttpMethod.Put;
        var url = isNew ? "api/banners" : $"api/banners/{model.BannerId!.Value}";
        var saved = await Send<BannerViewModel>(method, url, payload, ct);
        if (!saved.Ok || saved.Value is null) return RedirectWithError(saved.Error);

        var bannerId = saved.Value.BannerId;
        if (model.Image is not null)
        {
            var uploaded = await Upload(bannerId, model.Image, ct);
            if (!uploaded.Ok) return RedirectWithError($"Đã lưu nội dung nhưng tải ảnh thất bại: {uploaded.Error}");
        }

        if (model.IsVisible && (isNew || !saved.Value.IsVisible))
        {
            var shown = await Send<JsonElement>(HttpMethod.Patch, $"api/banners/{bannerId}/visibility", new { isVisible = true }, ct);
            if (!shown.Ok) return RedirectWithError($"Đã lưu banner nhưng chưa thể bật hiển thị: {shown.Error}");
        }

        TempData["SuccessMessage"] = isNew ? "Đã tạo banner mới thành công." : "Đã cập nhật banner thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool visible, string? version, CancellationToken ct)
    {
        var result = await Send<JsonElement>(HttpMethod.Patch, $"api/banners/{id}/visibility", new { isVisible = visible }, ct);
        SetMessage(result.Ok, result.Ok ? (visible ? "Đã bật hiển thị banner." : "Đã ẩn banner.") : result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, string placement, CancellationToken ct)
    {
        var list = await Send<List<BannerViewModel>>(HttpMethod.Get, "api/banners", null, ct);
        if (!list.Ok || list.Value is null) return RedirectWithError(list.Error);
        placement = string.IsNullOrWhiteSpace(placement) ? "HOME" : placement.Trim().ToUpperInvariant();
        var ordered = list.Value.Where(x => x.Placement == placement).OrderBy(x => x.SortOrder).Select(x => x.BannerId).ToList();
        var index = ordered.IndexOf(id); var target = index + Math.Sign(direction);
        if (index < 0 || target < 0 || target >= ordered.Count) return RedirectToAction(nameof(Index));
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        var result = await Send<JsonElement>(HttpMethod.Put, "api/banners/reorder", new { placement, bannerIds = ordered }, ct);
        SetMessage(result.Ok, result.Ok ? "Đã cập nhật thứ tự banner." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await Send<JsonElement>(HttpMethod.Delete, $"api/banners/{id}", null, ct);
        SetMessage(result.Ok, result.Ok ? "Đã xóa banner." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    private async Task<(bool Ok, T? Value, string Error)> Send<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(TokenCookie, out var token) || string.IsNullOrWhiteSpace(token)) return (false, default, "Phiên đăng nhập đã hết hạn.");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await _clients.CreateClient("RiversideApi").SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return (false, default, ReadMessage(text));
            var value = string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return (true, value, string.Empty);
        }
        catch (Exception) { return (false, default, "Không thể kết nối dịch vụ banner."); }
    }

    private async Task<(bool Ok, string Error)> Upload(int id, IFormFile image, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = image.OpenReadStream();
        using var file = new StreamContent(stream);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);
        content.Add(file, "image", Path.GetFileName(image.FileName));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/banners/{id}/image") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Request.Cookies[TokenCookie]);
        try
        {
            using var response = await _clients.CreateClient("RiversideApi").SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode ? (true, string.Empty) : (false, ReadMessage(text));
        }
        catch { return (false, "Không thể kết nối dịch vụ tải ảnh."); }
    }

    private IActionResult RedirectWithError(string error) { TempData["ErrorMessage"] = error; return RedirectToAction(nameof(Index)); }
    private void SetMessage(bool ok, string message) => TempData[ok ? "SuccessMessage" : "ErrorMessage"] = message;
    private static string ImageUrl(string? path, DateTime? updated) => path?.StartsWith("/uploads/banners/", StringComparison.Ordinal) == true ? $"/banner-images/{Uri.EscapeDataString(Path.GetFileName(path))}?v={(updated ?? DateTime.UnixEpoch).Ticks}" : path ?? "/images/home/hall-placeholder.jpg";
    private static string ReadMessage(string json) { try { return JsonDocument.Parse(json).RootElement.TryGetProperty("message", out var value) ? value.GetString() ?? "Thao tác thất bại." : "Thao tác thất bại."; } catch { return "Thao tác thất bại."; } }
}

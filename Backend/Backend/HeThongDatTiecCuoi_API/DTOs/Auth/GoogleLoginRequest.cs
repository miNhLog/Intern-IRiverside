using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed class GoogleLoginRequest
{
    [Required]
    public string AccessToken { get; init; } = string.Empty;
    public bool RememberMe { get; init; }
}

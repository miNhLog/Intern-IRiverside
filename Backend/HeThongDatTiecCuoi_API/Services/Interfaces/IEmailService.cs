namespace HeThongDatTiecCuoi_API.Services.Interfaces;

/// <summary>
/// FR15 / UC13: Contract đúng đường dẫn yêu cầu khóa luận (Services/Interfaces/IEmailService).
/// Kế thừa contract chính <see cref="Services.IEmailService"/> để không trùng lặp DI.
/// Mọi consumer nên inject <see cref="Services.IEmailService"/>.
/// </summary>
public interface IEmailService : Services.IEmailService
{
}

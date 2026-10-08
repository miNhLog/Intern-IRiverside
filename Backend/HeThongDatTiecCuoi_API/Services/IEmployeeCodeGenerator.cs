namespace HeThongDatTiecCuoi_API.Services;

public interface IEmployeeCodeGenerator
{
    Task<string> GenerateAsync(string roleName, CancellationToken cancellationToken);
}

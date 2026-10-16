using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class EmployeeCodeGenerator : IEmployeeCodeGenerator
{
    private readonly ApplicationDbContext _context;
    public EmployeeCodeGenerator(ApplicationDbContext context) => _context = context;

    public async Task<string> GenerateAsync(string roleName, CancellationToken cancellationToken)
    {
        var prefix = roleName switch
        {
            RoleNames.Admin => "AD",
            RoleNames.Manager => "QL",
            RoleNames.HallManager => "QLS",
            RoleNames.Coordinator => "DP",
            _ => "NV"
        };
        var codes = await _context.Employees.AsNoTracking()
            .Where(employee => employee.EmployeeCode.StartsWith(prefix))
            .Select(employee => employee.EmployeeCode)
            .ToListAsync(cancellationToken);
        var highestNumber = codes.Select(code => code[prefix.Length..])
            .Select(value => int.TryParse(value, out var number) ? number : 0)
            .DefaultIfEmpty().Max();
        var digits = roleName == RoleNames.Admin ? 4 : 3;
        return $"{prefix}{(highestNumber + 1).ToString($"D{digits}")}";
    }
}

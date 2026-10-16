using HeThongDatTiecCuoi_API.Data;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public interface IContractCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}

public sealed class ContractCodeGenerator : IContractCodeGenerator
{
    private readonly ApplicationDbContext _context;
    public ContractCodeGenerator(ApplicationDbContext context) => _context = context;

    // Caller opens a SERIALIZABLE transaction. The indexed range read and unique index
    // prevent two concurrent requests from reserving the same sequential code.
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var codes = await _context.Contracts.AsNoTracking()
            .Where(contract => contract.ContractCode.StartsWith("HD"))
            .Select(contract => contract.ContractCode)
            .ToListAsync(cancellationToken);
        var highest = codes.Select(code => code.Length > 2 && int.TryParse(code[2..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"HD{highest + 1:D5}";
    }
}

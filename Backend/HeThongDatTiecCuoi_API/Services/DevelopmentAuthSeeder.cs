using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class DevelopmentAuthSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<NguoiDung> _hasher;
    private readonly DevelopmentAccountsOptions _options;

    public DevelopmentAuthSeeder(
        ApplicationDbContext db,
        IPasswordHasher<NguoiDung> hasher,
        IOptions<DevelopmentAccountsOptions> options)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var roles = await EnsureRolesAsync(cancellationToken);
        await UpsertAccountAsync(_options.AdminEmail, _options.AdminPassword, roles[RoleNames.Admin], cancellationToken);
        await UpsertAccountAsync(_options.StaffEmail, _options.StaffPassword, roles[RoleNames.Staff], cancellationToken);
        await UpsertAccountAsync(_options.CustomerEmail, _options.CustomerPassword, roles[RoleNames.Customer], cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, VaiTro>> EnsureRolesAsync(CancellationToken cancellationToken)
    {
        var required = new[] { RoleNames.Admin, RoleNames.Staff, RoleNames.Customer };
        var roles = await _db.VaiTro
            .Where(x => required.Contains(x.TenVaiTro))
            .ToDictionaryAsync(x => x.TenVaiTro, cancellationToken);

        foreach (var name in required.Where(name => !roles.ContainsKey(name)))
        {
            var role = new VaiTro { TenVaiTro = name };
            _db.VaiTro.Add(role);
            roles[name] = role;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task UpsertAccountAsync(
        string email,
        string password,
        VaiTro role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.NguoiDung
            .Include(x => x.KhachHang)
            .Include(x => x.NhanVien)
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new NguoiDung
            {
                Email = normalizedEmail,
                VaiTro = role,
                VaiTroID = role.VaiTroID,
                TrangThai = "Hoạt động",
                NgayTao = DateTime.Now
            };
            _db.NguoiDung.Add(user);
        }
        else
        {
            user.VaiTro = role;
            user.VaiTroID = role.VaiTroID;
            user.TrangThai = "Hoạt động";
        }

        user.MatKhauHash = _hasher.HashPassword(user, password);

        if (role.TenVaiTro == RoleNames.Staff && user.NhanVien is null)
        {
            user.NhanVien = new NhanVien
            {
                NguoiDung = user,
                MaNhanVien = "NV001",
                HoTen = "Nhân viên tư vấn",
                SoDienThoai = "0912345678",
                TrangThai = "Đang làm việc"
            };
        }

        if (role.TenVaiTro == RoleNames.Customer && user.KhachHang is null)
        {
            user.KhachHang = new KhachHang
            {
                NguoiDung = user,
                HoTen = "Khách hàng mẫu",
                SoDienThoai = "0901234567"
            };
        }
    }
}

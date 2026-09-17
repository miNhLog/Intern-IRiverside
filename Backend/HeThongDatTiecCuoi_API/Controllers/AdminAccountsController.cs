using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminTaiKhoan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = "Quản trị viên")]
public sealed class AdminAccountsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<NguoiDung> _passwordHasher;

    public AdminAccountsController(
        ApplicationDbContext context,
        IPasswordHasher<NguoiDung> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }


    // GET: api/admin/accounts
    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAccounts(
        string? keyword,
        int? roleId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query =
            from nguoiDung in _context.NguoiDung.AsNoTracking()

            join vaiTro in _context.VaiTro.AsNoTracking()
                on nguoiDung.VaiTroID equals vaiTro.VaiTroID

            join nhanVienTam in _context.NhanVien.AsNoTracking()
                on nguoiDung.NguoiDungID equals nhanVienTam.NguoiDungID
                into nhomNhanVien

            from nhanVien in nhomNhanVien.DefaultIfEmpty()

            join khachHangTam in _context.KhachHang.AsNoTracking()
                on nguoiDung.NguoiDungID equals khachHangTam.NguoiDungID
                into nhomKhachHang

            from khachHang in nhomKhachHang.DefaultIfEmpty()

            select new
            {
                nguoiDung,
                vaiTro,
                nhanVien,
                khachHang
            };


        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();

            query = query.Where(x =>
                x.nguoiDung.Email.Contains(keyword) ||

                (x.nhanVien != null &&
                 (
                      (x.nhanVien.HoTen ?? "").Contains(keyword) ||
                      (x.nhanVien.MaNhanVien ?? "").Contains(keyword) ||
                      (x.nhanVien.SoDienThoai ?? "").Contains(keyword)
                 )) ||

                (x.khachHang != null &&
                 (
                      (x.khachHang.HoTen ?? "").Contains(keyword) ||
                      (x.khachHang.SoDienThoai ?? "").Contains(keyword)
                 )));
        }


        // LỌC VAI TRÒ
        if (roleId.HasValue)
        {
            query = query.Where(x =>
                x.nguoiDung.VaiTroID == roleId.Value);
        }


        // LỌC TRẠNG THÁI
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x =>
                x.nguoiDung.TrangThai == status);
        }


        var danhSach = await query
            .OrderByDescending(x => x.nguoiDung.NgayTao)
            .Select(x => new AccountDto
            {
                UserId = x.nguoiDung.NguoiDungID,

                Email = x.nguoiDung.Email,

                RoleId = x.nguoiDung.VaiTroID,

                RoleName = x.vaiTro.TenVaiTro,

                FullName = x.nhanVien != null
                    ? x.nhanVien.HoTen
                    : x.khachHang != null
                        ? x.khachHang.HoTen
                        : null,

                EmployeeCode = x.nhanVien != null
                    ? x.nhanVien.MaNhanVien
                    : null,

                PhoneNumber = x.nhanVien != null
                    ? x.nhanVien.SoDienThoai
                    : x.khachHang != null
                        ? x.khachHang.SoDienThoai
                        : null,

                Status = x.nguoiDung.TrangThai,

                CreatedAt = x.nguoiDung.NgayTao,
                EmployeeStatus = x.nhanVien != null
                    ? x.nhanVien.TrangThai
                    : null
            })
            .ToListAsync(cancellationToken);


        return Ok(danhSach);
    }
    // GET: api/admin/accounts/roles
    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles(
        CancellationToken cancellationToken)
    {
        var danhSach = await _context.VaiTro
            .AsNoTracking()
            .OrderBy(x => x.VaiTroID)
            .Select(x => new RoleDto
            {
                RoleId = x.VaiTroID,
                RoleName = x.TenVaiTro
            })
            .ToListAsync(cancellationToken);

        return Ok(danhSach);
    }

    // PATCH: api/admin/accounts/8/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateAccountStatus(
        int id,
        [FromBody] string status,
        CancellationToken cancellationToken)
    {
        var taiKhoan = await _context.NguoiDung
            .FirstOrDefaultAsync(
                x => x.NguoiDungID == id,
                cancellationToken);

        if (taiKhoan == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        var trangThaiHopLe = new[]
        {
            "Hoạt động",
            "Tạm khóa",
            "Ngừng hoạt động"
        };

        if (!trangThaiHopLe.Contains(status))
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        // Không cho Admin tự khóa chính tài khoản đang sử dụng
        var emailDangDangNhap =
            User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        if (taiKhoan.Email == emailDangDangNhap && status != "Hoạt động")
        {
            return BadRequest(new
            {
                message = "Không thể khóa hoặc ngừng hoạt động tài khoản đang đăng nhập."
            });
        }

        taiKhoan.TrangThai = status;

        await _context.SaveChangesAsync(cancellationToken);

        var message = status switch
        {
            "Hoạt động" => "Mở khóa tài khoản thành công.",
            "Tạm khóa" => "Tạm khóa tài khoản thành công.",
            "Ngừng hoạt động" => "Ngừng hoạt động tài khoản thành công.",
            _ => "Cập nhật trạng thái tài khoản thành công."
        };

        return Ok(new
        {
            message,
            userId = taiKhoan.NguoiDungID,
            status = taiKhoan.TrangThai
        });
    }
    // POST: api/admin/accounts/staff
    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaffAccount(
        [FromBody] CreateStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var hoTen = request.FullName.Trim();

        var soDienThoai = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(soDienThoai))
        {
            var soDienThoaiDaTonTai =
                await _context.NhanVien.AnyAsync(
                    x => x.SoDienThoai == soDienThoai,
                    cancellationToken
                );

            if (soDienThoaiDaTonTai)
            {
                return BadRequest(new
                {
                    message = "Số điện thoại này đã được sử dụng bởi nhân viên khác."
                });
            }
        }

        var maNhanVien = await TaoMaNhanVienAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email) ||
             string.IsNullOrWhiteSpace(hoTen) ||
             string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin bắt buộc."
            });
        }

        // Kiểm tra email
        var emailDaTonTai = await _context.NguoiDung
            .AnyAsync(
                x => x.Email == email,
                cancellationToken);

        if (emailDaTonTai)
        {
            return BadRequest(new
            {
                message = "Email đã được sử dụng."
            });
        }

        // Lấy vai trò từ DB
        var vaiTro = await _context.VaiTro
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.VaiTroID == request.RoleId,
                cancellationToken);

        if (vaiTro == null)
        {
            return BadRequest(new
            {
                message = "Vai trò không tồn tại."
            });
        }

        if (vaiTro.TenVaiTro != "Nhân viên tư vấn" & vaiTro.TenVaiTro != "Nhân viên điều phối")
        {
            return BadRequest(new
            {
                message = "Endpoint này chỉ dùng để tạo tài khoản nhân viên tư vấn hoặc nhân viên điều phối."
            });
        }

        // Admin chỉ tạo tài khoản nhân sự nội bộ
        if (vaiTro.TenVaiTro == "Khách hàng")
        {
            return BadRequest(new
            {
                message = "Tài khoản khách hàng phải được tạo qua chức năng đăng ký."
            });
        }

        // Kiểm tra mật khẩu giống yêu cầu đăng nhập hiện tại
        if (request.Password.Length < 8 ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit) ||
            !request.Password.Any(c => !char.IsLetterOrDigit(c)))
        {
            return BadRequest(new
            {
                message = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var nguoiDung = new NguoiDung
            {
            VaiTroID = request.RoleId,
                Email = email,
                TrangThai = "Hoạt động",
                NgayTao = DateTime.Now
            };

            nguoiDung.MatKhauHash =
                _passwordHasher.HashPassword(
                    nguoiDung,
                    request.Password);

            _context.NguoiDung.Add(nguoiDung);

            await _context.SaveChangesAsync(cancellationToken);


            var nhanVien = new NhanVien
            {
                NguoiDungID = nguoiDung.NguoiDungID,
                MaNhanVien = maNhanVien,
                HoTen = hoTen,
                SoDienThoai = soDienThoai,
                TrangThai = "Đang làm việc"
            };

            _context.NhanVien.Add(nhanVien);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                message = "Tạo tài khoản nhân viên thành công.",
                userId = nguoiDung.NguoiDungID,
                email = nguoiDung.Email,
                employeeCode = nhanVien.MaNhanVien,
                fullName = nhanVien.HoTen,
                role = vaiTro.TenVaiTro
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    // PUT: api/admin/accounts/staff/11
    [HttpPut("staff/{id}")]
    public async Task<IActionResult> UpdateStaffAccount(
        int id,
        [FromBody] UpdateStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var hoTen = request.FullName.Trim();

        var soDienThoai = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        if (!string.IsNullOrWhiteSpace(soDienThoai))
        {
            var soDienThoaiDaTonTai =
                await _context.NhanVien.AnyAsync(
                    x =>
                        x.SoDienThoai == soDienThoai &&
                        x.NguoiDungID != id,
                    cancellationToken
                );

            if (soDienThoaiDaTonTai)
            {
                return BadRequest(new
                {
                    message = "Số điện thoại này đã được sử dụng bởi nhân viên khác."
                });
            }
        }

        var trangThaiNhanVien = string.IsNullOrWhiteSpace(request.EmployeeStatus)
            ? null
            : request.EmployeeStatus.Trim();

        if (trangThaiNhanVien != null)
        {
                var trangThaiNhanVienHopLe = new[]
                {
                    "Đang làm việc",
                    "Tạm nghỉ",
                    "Đã nghỉ việc"
                };

                if (!trangThaiNhanVienHopLe.Contains(trangThaiNhanVien))
                {
                    return BadRequest(new
                    {
                        message = "Trạng thái nhân viên không hợp lệ."
                    });
                }
        }


        // 1. Kiểm tra dữ liệu bắt buộc
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(hoTen))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin bắt buộc."
            });
        }


        // 2. Kiểm tra độ dài đúng DB
        if (email.Length > 150)
        {
            return BadRequest(new
            {
                message = "Email không được vượt quá 150 ký tự."
            });
        }



        if (hoTen.Length > 150)
        {
            return BadRequest(new
            {
                message = "Họ tên không được vượt quá 150 ký tự."
            });
        }

        if (soDienThoai != null &&
            soDienThoai.Length > 20)
        {
            return BadRequest(new
            {
                message = "Số điện thoại không được vượt quá 20 ký tự."
            });
        }


        // 3. Tìm tài khoản
        var nguoiDung = await _context.NguoiDung
            .FirstOrDefaultAsync(
                x => x.NguoiDungID == id,
                cancellationToken);

        if (nguoiDung == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }


        // 4. Tìm hồ sơ nhân viên
        var nhanVien = await _context.NhanVien
            .FirstOrDefaultAsync(
                x => x.NguoiDungID == id,
                cancellationToken);

        if (nhanVien == null)
        {
            return BadRequest(new
            {
                message = "Tài khoản này không phải tài khoản nhân viên."
            });
        }


        // 5. Kiểm tra vai trò
        var vaiTro = await _context.VaiTro
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.VaiTroID == request.RoleId,
                cancellationToken);

        if (vaiTro == null)
        {
            return BadRequest(new
            {
                message = "Vai trò không tồn tại."
            });
        }


        // Chỉ cho nhân viên chuyển giữa Tư vấn và Điều phối
        if (vaiTro.TenVaiTro != "Nhân viên tư vấn" &&
            vaiTro.TenVaiTro != "Nhân viên điều phối")
        {
            return BadRequest(new
            {
                message = "Nhân viên chỉ có thể thuộc vai trò Tư vấn hoặc Điều phối."
            });
        }


        // 6. Email phải unique
        var emailDaTonTai = await _context.NguoiDung
            .AnyAsync(
                x => x.Email == email &&
                     x.NguoiDungID != id,
                cancellationToken);

        if (emailDaTonTai)
        {
            return BadRequest(new
            {
                message = "Email đã được sử dụng."
            });
        }


 


        // KHÔNG kiểm tra unique số điện thoại
        // vì DB hiện tại không quy định unique.


        // 8. Cập nhật
        nguoiDung.Email = email;
        nguoiDung.VaiTroID = request.RoleId;

        nhanVien.HoTen = hoTen;
        nhanVien.SoDienThoai = soDienThoai;

        if (trangThaiNhanVien != null)
        {
            nhanVien.TrangThai = trangThaiNhanVien;
        }


        await _context.SaveChangesAsync(cancellationToken);


        return Ok(new
        {
            message = "Cập nhật tài khoản nhân viên thành công.",

            userId = nguoiDung.NguoiDungID,

            email = nguoiDung.Email,

            employeeCode = nhanVien.MaNhanVien,

            fullName = nhanVien.HoTen,

            phoneNumber = nhanVien.SoDienThoai,

            role = vaiTro.TenVaiTro,

            accountStatus = nguoiDung.TrangThai,

            employeeStatus = nhanVien.TrangThai
        });
    }
    // PATCH: api/admin/accounts/11/reset-password
    [HttpPatch("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập mật khẩu mới."
            });
        }

        if (request.NewPassword.Length < 8 ||
            !request.NewPassword.Any(char.IsUpper) ||
            !request.NewPassword.Any(char.IsLower) ||
            !request.NewPassword.Any(char.IsDigit) ||
            !request.NewPassword.Any(c => !char.IsLetterOrDigit(c)))
        {
            return BadRequest(new
            {
                message = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            });
        }

        var nguoiDung = await _context.NguoiDung
            .FirstOrDefaultAsync(
                x => x.NguoiDungID == id,
                cancellationToken);

        if (nguoiDung == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        nguoiDung.MatKhauHash =
            _passwordHasher.HashPassword(
                nguoiDung,
                    request.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đặt lại mật khẩu thành công.",
            userId = nguoiDung.NguoiDungID,
            email = nguoiDung.Email
        });
    }
    private async Task<string> TaoMaNhanVienAsync(
    CancellationToken cancellationToken)
    {
        var danhSachMa = await _context.NhanVien
            .AsNoTracking()
            .Where(x => x.MaNhanVien.StartsWith("NV"))
            .Select(x => x.MaNhanVien)
            .ToListAsync(cancellationToken);

        var soLonNhat = 0;

        foreach (var ma in danhSachMa)
        {
            if (ma.Length <= 2)
            {
                continue;
            }

            var phanSo = ma.Substring(2);

            if (int.TryParse(phanSo, out var so) &&
                so > soLonNhat)
            {
                soLonNhat = so;
            }
        }

        return $"NV{soLonNhat + 1:D4}";
    }

    // PATCH: api/admin/accounts/staff/{userId}/status
    [HttpPatch("staff/{userId:int}/status")]
    public async Task<IActionResult> UpdateEmployeeStatus(
        int userId,
        [FromBody] string status,
        CancellationToken cancellationToken)
    {
        var trangThaiMoi = status?.Trim();

        var trangThaiHopLe = new[]
        {
        "Đang làm việc",
        "Tạm nghỉ",
        "Đã nghỉ việc"
    };

        if (string.IsNullOrWhiteSpace(trangThaiMoi) ||
            !trangThaiHopLe.Contains(trangThaiMoi))
        {
            return BadRequest(new
            {
                message = "Trạng thái nhân viên không hợp lệ."
            });
        }

        var nhanVien = await _context.NhanVien
            .FirstOrDefaultAsync(
                x => x.NguoiDungID == userId,
                cancellationToken
            );

        if (nhanVien == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy nhân viên."
            });
        }

        nhanVien.TrangThai = trangThaiMoi;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật trạng thái nhân viên thành công.",
            userId,
            employeeCode = nhanVien.MaNhanVien,
            employeeStatus = nhanVien.TrangThai
        });
    }
}

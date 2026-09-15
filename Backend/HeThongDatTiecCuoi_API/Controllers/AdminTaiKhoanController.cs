using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminTaiKhoan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Quản trị viên")]
public sealed class AdminTaiKhoanController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<NguoiDung> _passwordHasher;

    public AdminTaiKhoanController(
        ApplicationDbContext context,
        IPasswordHasher<NguoiDung> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }


    // GET: api/AdminTaiKhoan
    [HttpGet]
    public async Task<ActionResult<List<TaiKhoanDto>>> GetDanhSachTaiKhoan(
        string? tuKhoa,
        int? vaiTroId,
        string? trangThai,
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


        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            tuKhoa = tuKhoa.Trim();

            query = query.Where(x =>
                x.nguoiDung.Email.Contains(tuKhoa) ||

                (x.nhanVien != null &&
                 (
                     (x.nhanVien.HoTen ?? "").Contains(tuKhoa) ||
                     (x.nhanVien.MaNhanVien ?? "").Contains(tuKhoa) ||
                     (x.nhanVien.SoDienThoai ?? "").Contains(tuKhoa)
                 )) ||

                (x.khachHang != null &&
                 (
                     (x.khachHang.HoTen ?? "").Contains(tuKhoa) ||
                     (x.khachHang.SoDienThoai ?? "").Contains(tuKhoa)
                 )));
        }


        // LỌC VAI TRÒ
        if (vaiTroId.HasValue)
        {
            query = query.Where(x =>
                x.nguoiDung.VaiTroID == vaiTroId.Value);
        }


        // LỌC TRẠNG THÁI
        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(x =>
                x.nguoiDung.TrangThai == trangThai);
        }


        var danhSach = await query
            .OrderByDescending(x => x.nguoiDung.NgayTao)
            .Select(x => new TaiKhoanDto
            {
                NguoiDungID = x.nguoiDung.NguoiDungID,

                Email = x.nguoiDung.Email,

                VaiTroID = x.nguoiDung.VaiTroID,

                TenVaiTro = x.vaiTro.TenVaiTro,

                HoTen = x.nhanVien != null
                    ? x.nhanVien.HoTen
                    : x.khachHang != null
                        ? x.khachHang.HoTen
                        : null,

                MaNhanVien = x.nhanVien != null
                    ? x.nhanVien.MaNhanVien
                    : null,

                SoDienThoai = x.nhanVien != null
                    ? x.nhanVien.SoDienThoai
                    : x.khachHang != null
                        ? x.khachHang.SoDienThoai
                        : null,

                TrangThai = x.nguoiDung.TrangThai,

                NgayTao = x.nguoiDung.NgayTao,
                TrangThaiNhanVien = x.nhanVien != null
                    ? x.nhanVien.TrangThai
                    : null
            })
            .ToListAsync(cancellationToken);


        return Ok(danhSach);
    }
    // GET: api/AdminTaiKhoan/vai-tro
    [HttpGet("vai-tro")]
    public async Task<ActionResult<List<VaiTroDto>>> GetDanhSachVaiTro(
        CancellationToken cancellationToken)
    {
        var danhSach = await _context.VaiTro
            .AsNoTracking()
            .OrderBy(x => x.VaiTroID)
            .Select(x => new VaiTroDto
            {
                VaiTroID = x.VaiTroID,
                TenVaiTro = x.TenVaiTro
            })
            .ToListAsync(cancellationToken);

        return Ok(danhSach);
    }

    // PATCH: api/AdminTaiKhoan/8/trang-thai
    [HttpPatch("{id}/trang-thai")]
    public async Task<IActionResult> UpdateTrangThaiTaiKhoan(
        int id,
        [FromBody] string trangThai,
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

        if (!trangThaiHopLe.Contains(trangThai))
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        // Không cho Admin tự khóa chính tài khoản đang sử dụng
        var emailDangDangNhap =
            User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        if (taiKhoan.Email == emailDangDangNhap && trangThai != "Hoạt động")
        {
            return BadRequest(new
            {
                message = "Không thể khóa hoặc ngừng hoạt động tài khoản đang đăng nhập."
            });
        }

        taiKhoan.TrangThai = trangThai;

        await _context.SaveChangesAsync(cancellationToken);

        var message = trangThai switch
        {
            "Hoạt động" => "Mở khóa tài khoản thành công.",
            "Tạm khóa" => "Tạm khóa tài khoản thành công.",
            "Ngừng hoạt động" => "Ngừng hoạt động tài khoản thành công.",
            _ => "Cập nhật trạng thái tài khoản thành công."
        };

        return Ok(new
        {
            message,
            nguoiDungID = taiKhoan.NguoiDungID,
            trangThai = taiKhoan.TrangThai
        });
    }
    // POST: api/AdminTaiKhoan/nhan-vien
    [HttpPost("nhan-vien")]
    public async Task<IActionResult> TaoTaiKhoanNhanVien(
        [FromBody] TaoTaiKhoanNhanVienRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var hoTen = request.HoTen.Trim();

        var soDienThoai = string.IsNullOrWhiteSpace(request.SoDienThoai)
            ? null
            : request.SoDienThoai.Trim();

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
             string.IsNullOrWhiteSpace(request.MatKhau))
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
                x => x.VaiTroID == request.VaiTroID,
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
        if (request.MatKhau.Length < 8 ||
            !request.MatKhau.Any(char.IsUpper) ||
            !request.MatKhau.Any(char.IsLower) ||
            !request.MatKhau.Any(char.IsDigit) ||
            !request.MatKhau.Any(c => !char.IsLetterOrDigit(c)))
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
                VaiTroID = request.VaiTroID,
                Email = email,
                TrangThai = "Hoạt động",
                NgayTao = DateTime.Now
            };

            nguoiDung.MatKhauHash =
                _passwordHasher.HashPassword(
                    nguoiDung,
                    request.MatKhau);

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
                nguoiDungID = nguoiDung.NguoiDungID,
                email = nguoiDung.Email,
                maNhanVien = nhanVien.MaNhanVien,
                hoTen = nhanVien.HoTen,
                vaiTro = vaiTro.TenVaiTro
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    // PUT: api/AdminTaiKhoan/nhan-vien/11
    [HttpPut("nhan-vien/{id}")]
    public async Task<IActionResult> CapNhatTaiKhoanNhanVien(
        int id,
        [FromBody] CapNhatTaiKhoanNhanVienRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var hoTen = request.HoTen.Trim();

        var soDienThoai = string.IsNullOrWhiteSpace(request.SoDienThoai)
            ? null
            : request.SoDienThoai.Trim();
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

        var trangThaiNhanVien = string.IsNullOrWhiteSpace(request.TrangThaiNhanVien)
            ? null
            : request.TrangThaiNhanVien.Trim();

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
                x => x.VaiTroID == request.VaiTroID,
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
        nguoiDung.VaiTroID = request.VaiTroID;

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

            nguoiDungID = nguoiDung.NguoiDungID,

            email = nguoiDung.Email,

            maNhanVien = nhanVien.MaNhanVien,

            hoTen = nhanVien.HoTen,

            soDienThoai = nhanVien.SoDienThoai,

            vaiTro = vaiTro.TenVaiTro,

            trangThaiTaiKhoan = nguoiDung.TrangThai,

            trangThaiNhanVien = nhanVien.TrangThai
        });
    }
    // PATCH: api/AdminTaiKhoan/11/dat-lai-mat-khau
    [HttpPatch("{id}/dat-lai-mat-khau")]
    public async Task<IActionResult> DatLaiMatKhau(
        int id,
        [FromBody] DatLaiMatKhauRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MatKhauMoi))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập mật khẩu mới."
            });
        }

        if (request.MatKhauMoi.Length < 8 ||
            !request.MatKhauMoi.Any(char.IsUpper) ||
            !request.MatKhauMoi.Any(char.IsLower) ||
            !request.MatKhauMoi.Any(char.IsDigit) ||
            !request.MatKhauMoi.Any(c => !char.IsLetterOrDigit(c)))
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
                request.MatKhauMoi);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đặt lại mật khẩu thành công.",
            nguoiDungID = nguoiDung.NguoiDungID,
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

    // PATCH:
    // api/AdminTaiKhoan/nhan-vien/{nguoiDungId}/trang-thai
    [HttpPatch("nhan-vien/{nguoiDungId:int}/trang-thai")]
    public async Task<IActionResult> CapNhatTrangThaiNhanVien(
        int nguoiDungId,
        [FromBody] string trangThai,
        CancellationToken cancellationToken)
    {
        var trangThaiMoi = trangThai?.Trim();

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
                x => x.NguoiDungID == nguoiDungId,
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
            nguoiDungID = nguoiDungId,
            maNhanVien = nhanVien.MaNhanVien,
            trangThaiNhanVien = nhanVien.TrangThai
        });
    }
}
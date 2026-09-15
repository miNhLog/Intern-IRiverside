using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class LichSanhController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LichSanhController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/LichSanh/tuan?ngayBatDau=2026-11-16
    // GET: api/LichSanh/tuan?ngayBatDau=2026-11-16&sanhTiecId=1
    [HttpGet("tuan")]
    public async Task<IActionResult> GetTheoTuan(
        DateTime ngayBatDau,
        int? sanhTiecId = null)
    {
        var ngayDauTuan = ngayBatDau.Date;
        var ngayCuoiTuan = ngayDauTuan.AddDays(6);

        var danhSachSanh = _context.SanhTiec
            .AsQueryable();

        if (sanhTiecId.HasValue)
        {
            danhSachSanh = danhSachSanh
                .Where(x => x.SanhTiecID == sanhTiecId.Value);
        }

        var sanhs = await danhSachSanh
            .OrderBy(x => x.SanhTiecID)
            .ToListAsync();

        if (sanhs.Count == 0)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh."
            });
        }

        await TuDongTaoLichTheoTuan(sanhs, ngayDauTuan);

        var danhSachLich = await _context.LichSanh
            .Include(x => x.SanhTiec)
            .Where(x =>
                x.Ngay >= ngayDauTuan &&
                x.Ngay <= ngayCuoiTuan &&
                (!sanhTiecId.HasValue || x.SanhTiecID == sanhTiecId.Value))
            .OrderBy(x => x.SanhTiecID)
            .ThenBy(x => x.Ngay)
            .ThenBy(x => x.CaToChuc)
            .ToListAsync();

        var danhSachDatTiec = await _context.DatTiec
            .Include(x => x.KhachHang)
            .Where(x =>
                x.LichSanh.Ngay >= ngayDauTuan &&
                x.LichSanh.Ngay <= ngayCuoiTuan &&
                x.TrangThai != "Đã hủy")
            .ToListAsync();

        var ketQua = sanhs.Select(sanh => new
        {
            sanhTiecID = sanh.SanhTiecID,
            maSanh = sanh.MaSanh,
            tenSanh = sanh.TenSanh,
            trangThaiSanh = sanh.TrangThai,

            lich = danhSachLich
        .Where(x => x.SanhTiecID == sanh.SanhTiecID)
        .Select(x =>
        {
            var datTiec = danhSachDatTiec
                .FirstOrDefault(d => d.LichSanhID == x.LichSanhID);

            return new
            {
                lichSanhID = x.LichSanhID,
                ngay = x.Ngay,
                caToChuc = x.CaToChuc,
                trangThai = datTiec != null ? "Đã đặt" : x.TrangThai,
                ghiChu = x.GhiChu,

                booking = datTiec == null
                    ? null
                    : new
                    {
                        datTiecID = datTiec.DatTiecID,
                        maDatTiec = datTiec.MaDatTiec,
                        hoTenKhachHang = datTiec.KhachHang.HoTen,
                        soBan = datTiec.SoBan,
                        soLuongKhach = datTiec.SoLuongKhach,
                        trangThaiDatTiec = datTiec.TrangThai
                    }
            };
        })
        .ToList()
        });

        return Ok(new
        {
            tuNgay = ngayDauTuan,
            denNgay = ngayCuoiTuan,
            danhSachSanh = ketQua
        });
    }

    // PATCH: api/LichSanh/10/trang-thai
    [HttpPatch("{id}/trang-thai")]
    public async Task<IActionResult> UpdateTrangThai(
        int id,
        [FromBody] string trangThai)
    {
        var lich = await _context.LichSanh.FindAsync(id);

        if (lich == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy lịch sảnh."
            });
        }

        var coBooking = await _context.DatTiec
            .AnyAsync(x =>
                x.LichSanhID == id &&
                x.TrangThai != "Đã hủy");

        if (coBooking)
        {
            return BadRequest(new
            {
                message = "Không thể chỉnh trạng thái vì ca này đã có booking."
            });
        }

        if (trangThai != "Trống" &&
            trangThai != "Tạm khóa")
        {
            return BadRequest(new
            {
                message = "Admin chỉ được chuyển trạng thái giữa Trống và Tạm khóa."
            });
        }

        lich.TrangThai = trangThai;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái lịch sảnh thành công.",
            lich
        });
    }

    private async Task TuDongTaoLichTheoTuan(
        List<SanhTiec> danhSachSanh,
        DateTime ngayBatDau)
    {
        var ngayCuoiTuan = ngayBatDau.AddDays(6);

        var lichDaCo = await _context.LichSanh
            .Where(x =>
                x.Ngay >= ngayBatDau &&
                x.Ngay <= ngayCuoiTuan)
            .ToListAsync();

        var caToChuc = new[]
        {
            "Ca trưa",
            "Ca tối"
        };

        foreach (var sanh in danhSachSanh)
        {
            for (var i = 0; i < 7; i++)
            {
                var ngay = ngayBatDau.AddDays(i);

                foreach (var ca in caToChuc)
                {
                    var daTonTai = lichDaCo.Any(x =>
                        x.SanhTiecID == sanh.SanhTiecID &&
                        x.Ngay.Date == ngay.Date &&
                        x.CaToChuc == ca);

                    if (daTonTai)
                    {
                        continue;
                    }

                    _context.LichSanh.Add(new LichSanh
                    {
                        SanhTiecID = sanh.SanhTiecID,
                        Ngay = ngay,
                        CaToChuc = ca,
                        TrangThai = "Trống"
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
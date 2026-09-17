using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.BanquetHalls;
using HeThongDatTiecCuoi_API.DTOs.HallSchedules;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/hall-schedules")]
[Authorize(Roles = RoleNames.Admin)]
public class HallSchedulesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HallSchedulesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/hall-schedules/week?startDate=2026-11-16
    // GET: api/hall-schedules/week?startDate=2026-11-16&hallId=1
    [HttpGet("week")]
    public async Task<IActionResult> GetWeeklySchedule(
        DateTime startDate,
        int? hallId = null)
    {
        var ngayDauTuan = startDate.Date;
        var ngayCuoiTuan = ngayDauTuan.AddDays(6);

        var danhSachSanh = _context.SanhTiec
            .AsQueryable();

        if (hallId.HasValue)
        {
            danhSachSanh = danhSachSanh
                .Where(x => x.SanhTiecID == hallId.Value);
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
                (!hallId.HasValue || x.SanhTiecID == hallId.Value))
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

        var ketQua = sanhs.Select(sanh => new HallScheduleSummaryDto
        {
            HallId = sanh.SanhTiecID,
            HallCode = sanh.MaSanh,
            HallName = sanh.TenSanh,
            HallStatus = sanh.TrangThai,

            Slots = danhSachLich
        .Where(x => x.SanhTiecID == sanh.SanhTiecID)
        .Select(x =>
        {
            var datTiec = danhSachDatTiec
                .FirstOrDefault(d => d.LichSanhID == x.LichSanhID);

            return new ScheduleSlotDto
            {
                ScheduleId = x.LichSanhID,
                Date = x.Ngay,
                EventSession = x.CaToChuc,
                Status = datTiec != null ? "Đã đặt" : x.TrangThai,
                Note = x.GhiChu,

                Booking = datTiec == null
                    ? null
                    : new BookingSummaryDto
                    {
                        BookingId = datTiec.DatTiecID,
                        BookingCode = datTiec.MaDatTiec,
                        CustomerName = datTiec.KhachHang.HoTen,
                        TableCount = datTiec.SoBan,
                        GuestCount = datTiec.SoLuongKhach,
                        BookingStatus = datTiec.TrangThai
                    }
            };
        })
        .ToList()
        });

        return Ok(new WeeklyHallScheduleResponse
        {
            StartDate = ngayDauTuan,
            EndDate = ngayCuoiTuan,
            Halls = ketQua.ToList()
        });
    }

    // PATCH: api/hall-schedules/10/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] string status)
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

        if (status != "Trống" &&
            status != "Tạm khóa")
        {
            return BadRequest(new
            {
                message = "Admin chỉ được chuyển trạng thái giữa Trống và Tạm khóa."
            });
        }

        lich.TrangThai = status;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái lịch sảnh thành công.",
            schedule = ToDto(lich)
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

    private static ScheduleDetailDto ToDto(LichSanh lich) => new()
    {
        ScheduleId = lich.LichSanhID,
        HallId = lich.SanhTiecID,
        Date = lich.Ngay,
        EventSession = lich.CaToChuc,
        Status = lich.TrangThai,
        Note = lich.GhiChu,
        Hall = lich.SanhTiec is null
            ? null
            : new BanquetHallDto
            {
                HallId = lich.SanhTiec.SanhTiecID,
                HallCode = lich.SanhTiec.MaSanh,
                HallName = lich.SanhTiec.TenSanh,
                MinCapacity = lich.SanhTiec.SucChuaToiThieu,
                MaxCapacity = lich.SanhTiec.SucChuaToiDa,
                RentalPrice = lich.SanhTiec.GiaThue,
                Description = lich.SanhTiec.MoTa,
                ImageUrl = lich.SanhTiec.HinhAnh,
                Status = lich.SanhTiec.TrangThai
            }
    };
}

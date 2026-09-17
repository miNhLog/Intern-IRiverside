using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.BanquetHalls;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/banquet-halls")]
[Authorize(Roles = RoleNames.Admin)]
public class BanquetHallsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BanquetHallsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/banquet-halls
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var danhSachSanh = await _context.SanhTiec
            .OrderBy(x => x.SanhTiecID)
            .ToListAsync();

        return Ok(danhSachSanh.Select(ToDto));
    }

    // GET: api/banquet-halls/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var sanh = await _context.SanhTiec.FindAsync(id);

        if (sanh == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        return Ok(ToDto(sanh));
    }

    // POST: api/banquet-halls
    [HttpPost]
    public async Task<IActionResult> Create(BanquetHallDto model)
    {
        var sanhTiec = new SanhTiec
        {
            SanhTiecID = model.HallId,
            MaSanh = model.HallCode,
            TenSanh = model.HallName,
            SucChuaToiThieu = model.MinCapacity,
            SucChuaToiDa = model.MaxCapacity,
            GiaThue = model.RentalPrice,
            MoTa = model.Description,
            HinhAnh = model.ImageUrl,
            TrangThai = model.Status
        };

        if (string.IsNullOrWhiteSpace(sanhTiec.MaSanh))
        {
            return BadRequest(new
            {
                message = "Mã sảnh không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(sanhTiec.TenSanh))
        {
            return BadRequest(new
            {
                message = "Tên sảnh không được để trống."
            });
        }

        var maSanhDaTonTai = await _context.SanhTiec
            .AnyAsync(x => x.MaSanh == sanhTiec.MaSanh);

        if (maSanhDaTonTai)
        {
            return BadRequest(new
            {
                message = "Mã sảnh đã tồn tại."
            });
        }

        if (sanhTiec.SucChuaToiDa <= 0)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (sanhTiec.SucChuaToiThieu.HasValue &&
            sanhTiec.SucChuaToiThieu.Value > sanhTiec.SucChuaToiDa)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (sanhTiec.GiaThue < 0)
        {
            return BadRequest(new
            {
                message = "Giá thuê không hợp lệ."
            });
        }

        if (sanhTiec.TrangThai != "Hoạt động" &&
            sanhTiec.TrangThai != "Bảo trì" &&
            sanhTiec.TrangThai != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        _context.SanhTiec.Add(sanhTiec);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = sanhTiec.SanhTiecID },
            ToDto(sanhTiec)
        );
    }

    // PUT: api/banquet-halls/1
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BanquetHallDto model)
    {
        var sanhHienTai = await _context.SanhTiec.FindAsync(id);

        if (sanhHienTai == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        var maSanhDaTonTai = await _context.SanhTiec
            .AnyAsync(x =>
                x.MaSanh == model.HallCode &&
                x.SanhTiecID != id);

        if (maSanhDaTonTai)
        {
            return BadRequest(new
            {
                message = "Mã sảnh đã tồn tại."
            });
        }

        if (model.MaxCapacity <= 0)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (model.MinCapacity.HasValue &&
            model.MinCapacity.Value > model.MaxCapacity)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (model.RentalPrice < 0)
        {
            return BadRequest(new
            {
                message = "Giá thuê không hợp lệ."
            });
        }

        if (model.Status != "Hoạt động" &&
            model.Status != "Bảo trì" &&
            model.Status != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        sanhHienTai.MaSanh = model.HallCode;
        sanhHienTai.TenSanh = model.HallName;
        sanhHienTai.SucChuaToiThieu = model.MinCapacity;
        sanhHienTai.SucChuaToiDa = model.MaxCapacity;
        sanhHienTai.GiaThue = model.RentalPrice;
        sanhHienTai.MoTa = model.Description;
        sanhHienTai.HinhAnh = model.ImageUrl;
        sanhHienTai.TrangThai = model.Status;

        await _context.SaveChangesAsync();

        return Ok(ToDto(sanhHienTai));
    }

    // PATCH: api/banquet-halls/1/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] string status)
    {
        var sanh = await _context.SanhTiec.FindAsync(id);

        if (sanh == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        if (status != "Hoạt động" &&
            status != "Bảo trì" &&
            status != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        sanh.TrangThai = status;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái thành công.",
            hall = ToDto(sanh)
        });
    }

    // DELETE: api/banquet-halls/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sanh = await _context.SanhTiec.FindAsync(id);

        if (sanh == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        if (sanh.TrangThai == "Hoạt động")
        {
            return BadRequest(new
            {
                message = "Không thể xóa sảnh đang hoạt động. Hãy chuyển sảnh sang trạng thái Ngừng hoạt động trước."
            });
        }

        var coBooking = await _context.DatTiec
            .AnyAsync(x =>
                x.LichSanh.SanhTiecID == id);

        if (coBooking)
        {
            return BadRequest(new
            {
                message = "Không thể xóa sảnh vì sảnh đã từng có booking."
            });
        }

        var danhSachLich = await _context.LichSanh
            .Where(x => x.SanhTiecID == id)
            .ToListAsync();

        _context.LichSanh.RemoveRange(danhSachLich);
        _context.SanhTiec.Remove(sanh);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa sảnh thành công."
        });
    }

    private static BanquetHallDto ToDto(SanhTiec sanh) => new()
    {
        HallId = sanh.SanhTiecID,
        HallCode = sanh.MaSanh,
        HallName = sanh.TenSanh,
        MinCapacity = sanh.SucChuaToiThieu,
        MaxCapacity = sanh.SucChuaToiDa,
        RentalPrice = sanh.GiaThue,
        Description = sanh.MoTa,
        ImageUrl = sanh.HinhAnh,
        Status = sanh.TrangThai
    };
}

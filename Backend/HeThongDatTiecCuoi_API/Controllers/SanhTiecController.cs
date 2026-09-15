using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class SanhTiecController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SanhTiecController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/SanhTiec
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var danhSachSanh = await _context.SanhTiec
            .OrderBy(x => x.SanhTiecID)
            .ToListAsync();

        return Ok(danhSachSanh);
    }

    // GET: api/SanhTiec/1
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

        return Ok(sanh);
    }

    // POST: api/SanhTiec
    [HttpPost]
    public async Task<IActionResult> Create(SanhTiec sanhTiec)
    {
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
            sanhTiec
        );
    }

    // PUT: api/SanhTiec/1
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, SanhTiec sanhTiec)
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
                x.MaSanh == sanhTiec.MaSanh &&
                x.SanhTiecID != id);

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

        sanhHienTai.MaSanh = sanhTiec.MaSanh;
        sanhHienTai.TenSanh = sanhTiec.TenSanh;
        sanhHienTai.SucChuaToiThieu = sanhTiec.SucChuaToiThieu;
        sanhHienTai.SucChuaToiDa = sanhTiec.SucChuaToiDa;
        sanhHienTai.GiaThue = sanhTiec.GiaThue;
        sanhHienTai.MoTa = sanhTiec.MoTa;
        sanhHienTai.HinhAnh = sanhTiec.HinhAnh;
        sanhHienTai.TrangThai = sanhTiec.TrangThai;

        await _context.SaveChangesAsync();

        return Ok(sanhHienTai);
    }

    // PATCH: api/SanhTiec/1/trang-thai
    [HttpPatch("{id}/trang-thai")]
    public async Task<IActionResult> UpdateTrangThai(
        int id,
        [FromBody] string trangThai)
    {
        var sanh = await _context.SanhTiec.FindAsync(id);

        if (sanh == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        if (trangThai != "Hoạt động" &&
            trangThai != "Bảo trì" &&
            trangThai != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        sanh.TrangThai = trangThai;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái thành công.",
            sanh
        });
    }

    // DELETE: api/SanhTiec/1
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
}
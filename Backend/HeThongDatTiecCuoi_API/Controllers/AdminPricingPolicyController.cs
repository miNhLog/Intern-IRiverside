using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.PricingPolicy;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Controllers
{
    [Route("api/admin/pricing-policy")]
    [ApiController]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public class AdminPricingPolicyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminPricingPolicyController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================= TAB 1: DỮ LIỆU ĐIỀU KHOẢN HỢP ĐỒNG MẪU =================
        private static readonly List<PaymentPolicyDto> DefaultPolicies = new()
        {
            new PaymentPolicyDto
            {
                Id = 1,
                MaQuyDinh = "POL-PAY-01",
                TenDieuKhoan = "Đặt cọc Đợt 1 - Giữ chỗ sảnh tiệc",
                PhanLoai = "Đặt cọc & Giữ chỗ",
                DinhMuc = "20% Tổng dự toán",
                ThoiHan = "Trong 48 giờ",
                CanCu = "Quy chuẩn giữ chỗ 48h",
                MoTa = "Khách hàng hoàn tất 20% chi phí dự toán để xác lập hợp đồng chính thức. Quá 48 giờ kể từ khi tạo booking nếu chưa nhận cọc, hệ thống tự động hủy giữ chỗ và mở lại lịch sảnh khả dụng.",
                TrangThai = "Hiệu lực"
            },
            new PaymentPolicyDto
            {
                Id = 2,
                MaQuyDinh = "POL-PAY-02",
                TenDieuKhoan = "Thanh toán Đợt 2 - Chốt thực đơn & dịch vụ",
                PhanLoai = "Thanh toán đợt",
                DinhMuc = "50% Tổng giá trị",
                ThoiHan = "Trước tiệc 7 – 10 ngày",
                CanCu = "Quy trình thanh toán 3 đợt",
                MoTa = "Khách hàng chốt chính thức số lượng bàn phát sinh, danh mục ẩm thực và hoàn tất thanh toán lũy kế đạt tối thiểu 70% giá trị hợp đồng.",
                TrangThai = "Hiệu lực"
            },
            new PaymentPolicyDto
            {
                Id = 3,
                MaQuyDinh = "POL-PAY-03",
                TenDieuKhoan = "Quyết toán Đợt 3 - Sau sự kiện",
                PhanLoai = "Quyết toán thanh lý",
                DinhMuc = "100% Số dư công nợ",
                ThoiHan = "Ngay khi tan tiệc",
                CanCu = "Thanh lý & quyết toán",
                MoTa = "Đối soát các chi phí phát sinh thực tế (bàn dự phòng, thức uống gọi thêm, phụ phí dịch vụ) và thanh toán toàn bộ số tiền còn lại ngay sau tiệc.",
                TrangThai = "Hiệu lực"
            },
            new PaymentPolicyDto
            {
                Id = 4,
                MaQuyDinh = "POL-CNC-04",
                TenDieuKhoan = "Chính sách bồi hoàn & hủy tiệc cưới",
                PhanLoai = "Hủy hợp đồng",
                DinhMuc = "Phạt 50% - 100% Cọc",
                ThoiHan = "Theo mốc ngày hủy",
                CanCu = "Bồi thường hợp đồng kinh tế",
                MoTa = "Hủy trước 60 ngày: phạt 50% tiền cọc Đợt 1; Hủy từ 30 đến 59 ngày: phạt 100% tiền cọc Đợt 1; Hủy dưới 30 ngày: bồi hoàn 50% toàn bộ giá trị hợp đồng.",
                TrangThai = "Hiệu lực"
            },
            new PaymentPolicyDto
            {
                Id = 5,
                MaQuyDinh = "POL-CHG-05",
                TenDieuKhoan = "Chính sách dời ngày & chuyển sảnh tiệc",
                PhanLoai = "Thay đổi lịch trình",
                DinhMuc = "Miễn phí / Phụ thu lệch giá",
                ThoiHan = "Báo trước 45 ngày",
                CanCu = "Biến động lịch tiệc",
                MoTa = "Báo trước từ 45 ngày: miễn phí chuyển ngày hoặc sảnh lần đầu (chỉ thanh toán chênh lệch giá sảnh mới). Báo dưới 30 ngày phụ thu 5.000.000 VNĐ phí vận hành.",
                TrangThai = "Hiệu lực"
            }
        };

        // GET: api/admin/pricing-policy/payment-terms
        [HttpGet("payment-terms")]
        public IActionResult GetPaymentTerms()
        {
            return Ok(new { success = true, data = DefaultPolicies });
        }

        // GET: api/admin/pricing-policy/payment-terms/{id} (Chi tiết chính sách)
        [HttpGet("payment-terms/{id}")]
        public IActionResult GetPaymentTermDetail(int id)
        {
            var item = DefaultPolicies.FirstOrDefault(x => x.Id == id);
            if (item == null) return NotFound(new { success = false, message = "Không tìm thấy điều khoản." });
            return Ok(new { success = true, data = item });
        }

        // PUT: api/admin/pricing-policy/payment-terms/{id}
        [HttpPut("payment-terms/{id}")]
        public IActionResult UpdatePaymentTerm(int id, [FromBody] UpdatePaymentPolicyRequest req)
        {
            var item = DefaultPolicies.FirstOrDefault(x => x.Id == id);
            if (item == null) return NotFound(new { success = false, message = "Không tìm thấy điều khoản." });

            item.TenDieuKhoan = req.TenDieuKhoan;
            item.DinhMuc = req.DinhMuc;
            item.ThoiHan = req.ThoiHan;
            item.MoTa = req.MoTa;

            return Ok(new { success = true, message = "Cập nhật điều khoản thành công!" });
        }

        // ================= TAB 2: TRUY VẤN BẢNG GIÁ ĐẠI SẢNH (TỪ SANHTIEC) =================
        // GET: api/admin/pricing-policy/hall-shifts
        [HttpGet("hall-shifts")]
        public async Task<IActionResult> GetHallShiftPricings()
        {
            var halls = await _context.Halls
                .Include(h => h.Status) // Nạp bảng Status liên kết
                .AsNoTracking()
                .Where(h => h.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                .OrderBy(h => h.HallId)
                .ToListAsync();

            var result = halls.Select(h => new HallShiftPricingDto
            {
                SanhTiecId = h.HallId,
                MaSanh = h.HallCode,
                TenSanh = h.HallName,
                SucChuaToiThieu = h.MinimumCapacity,
                SucChuaToiDa = h.MaximumCapacity,
                GiaGoc = h.RentalPrice,
                GiaCaTrua = h.RentalPrice,
                GiaCaToi = h.RentalPrice * 1.15m,
                GiaCuoiTuan = h.RentalPrice * 1.25m,
                // Sửa tại đây: Lấy StatusName (hoặc TenTrangThai nếu model đặt tiếng Việt)
                TrangThai = h.Status != null ? h.Status.StatusName : "Hoạt động"
            }).ToList();

            return Ok(new { success = true, data = result });
        }

        // GET: api/admin/pricing-policy/hall-shifts/{id}
        [HttpGet("hall-shifts/{id}")]
        public async Task<IActionResult> GetHallShiftDetail(int id)
        {
            var hall = await _context.Halls
                .Include(h => h.Status)
                .FirstOrDefaultAsync(h =>
                    h.HallId == id &&
                    h.DataStatus.DataStatusCode == DataStatusCodes.Existing);

            if (hall == null)
                return NotFound(new { success = false, message = "Không tìm thấy thông tin sảnh." });

            var detail = new HallShiftPricingDto
            {
                SanhTiecId = hall.HallId,
                MaSanh = hall.HallCode,
                TenSanh = hall.HallName,
                SucChuaToiThieu = hall.MinimumCapacity,
                SucChuaToiDa = hall.MaximumCapacity,
                GiaGoc = hall.RentalPrice,
                GiaCaTrua = hall.RentalPrice,
                GiaCaToi = hall.RentalPrice * 1.15m,
                GiaCuoiTuan = hall.RentalPrice * 1.25m,
                // Sửa tại đây:
                TrangThai = hall.Status != null ? hall.Status.StatusName : "Hoạt động"
            };

            return Ok(new { success = true, data = detail });
        }

        // PUT: api/admin/pricing-policy/hall-price/{sanhId} (Cập nhật đơn giá sảnh)
        [HttpPut("hall-price/{sanhId}")]
        public async Task<IActionResult> UpdateHallBasePrice(int sanhId, [FromBody] UpdateHallPriceRequest req)
        {
            var hall = await _context.Halls.FirstOrDefaultAsync(h =>
                h.HallId == sanhId &&
                h.DataStatus.DataStatusCode == DataStatusCodes.Existing);
            if (hall == null)
                return NotFound(new { success = false, message = "Không tìm thấy đại sảnh." });

            if (req.GiaThueMoi < 0)
                return BadRequest(new { success = false, message = "Đơn giá thuê không hợp lệ." });

            hall.RentalPrice = req.GiaThueMoi;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Cập nhật đơn giá sảnh {hall.HallName} thành công!" });
        }
    }
}

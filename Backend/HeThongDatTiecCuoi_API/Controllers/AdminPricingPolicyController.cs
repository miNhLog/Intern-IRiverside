using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.PricingPolicy;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using System.Security.Claims;
using System.Text.Json;

namespace HeThongDatTiecCuoi_API.Controllers
{
    [Route("api/admin/pricing-policy")]
    [ApiController]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public class AdminPricingPolicyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentPolicyProvider _paymentPolicies;
        private readonly IHallPricingPolicyService _hallPricing;

        public AdminPricingPolicyController(
            ApplicationDbContext context,
            IPaymentPolicyProvider paymentPolicies,
            IHallPricingPolicyService hallPricing)
        {
            _context = context;
            _paymentPolicies = paymentPolicies;
            _hallPricing = hallPricing;
        }

        // ================= TAB 1: DỮ LIỆU ĐIỀU KHOẢN HỢP ĐỒNG MẪU =================
        // GET: api/admin/pricing-policy/payment-terms
        [HttpGet("payment-terms")]
        public IActionResult GetPaymentTerms()
        {
            return Ok(new { success = true, data = _paymentPolicies.GetAll() });
        }

        // GET: api/admin/pricing-policy/payment-terms/{id} (Chi tiết chính sách)
        [HttpGet("payment-terms/{id}")]
        public IActionResult GetPaymentTermDetail(int id)
        {
            var item = _paymentPolicies.GetById(id);
            if (item == null) return NotFound(new { success = false, message = "Không tìm thấy điều khoản." });
            return Ok(new { success = true, data = item });
        }

        // PUT: api/admin/pricing-policy/payment-terms/{id}
        [HttpPut("payment-terms/{id}")]
        public IActionResult UpdatePaymentTerm(int id, [FromBody] UpdatePaymentPolicyRequest req)
        {
            if (!_paymentPolicies.Update(id, req))
                return NotFound(new { success = false, message = "Không tìm thấy điều khoản." });

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

            var result = halls.Select(h => {
                var prices = _hallPricing.GetShiftPriceBreakdown(h.RentalPrice);
                return new HallShiftPricingDto
                {
                    SanhTiecId = h.HallId,
                    MaSanh = h.HallCode,
                    TenSanh = h.HallName,
                    SucChuaToiThieu = h.MinimumCapacity,
                    SucChuaToiDa = h.MaximumCapacity,
                    GiaGoc = prices.BasePrice,
                    GiaCaTrua = prices.NoonPrice,
                    GiaCaToi = prices.EveningPrice,
                    GiaCuoiTuan = prices.WeekendPrice,
                    // Sửa tại đây: Lấy StatusName (hoặc TenTrangThai nếu model đặt tiếng Việt)
                    TrangThai = h.Status != null ? h.Status.StatusName : "Hoạt động"
                };
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

            var prices = _hallPricing.GetShiftPriceBreakdown(hall.RentalPrice);
            var detail = new HallShiftPricingDto
            {
                SanhTiecId = hall.HallId,
                MaSanh = hall.HallCode,
                TenSanh = hall.HallName,
                SucChuaToiThieu = hall.MinimumCapacity,
                SucChuaToiDa = hall.MaximumCapacity,
                GiaGoc = prices.BasePrice,
                GiaCaTrua = prices.NoonPrice,
                GiaCaToi = prices.EveningPrice,
                GiaCuoiTuan = prices.WeekendPrice,
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

            var oldPrice = hall.RentalPrice;
            if (oldPrice == req.GiaThueMoi)
                return Ok(new { success = true, unchanged = true, message = "Đơn giá sảnh không thay đổi." });

            hall.RentalPrice = req.GiaThueMoi;
            var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActorId) ? parsedActorId : (int?)null;
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = AuditActions.HallPriceUpdated,
                EntityName = AuditEntityNames.Hall, EntityId = hall.HallId,
                OldData = JsonSerializer.Serialize(new { hall.HallCode, hall.HallName, RentalPrice = oldPrice }),
                NewData = JsonSerializer.Serialize(new { hall.HallCode, hall.HallName, RentalPrice = req.GiaThueMoi }),
                Timestamp = DateTime.Now, Outcome = AuditOutcomes.Success,
                Notes = $"Cập nhật giá thuê sảnh {hall.HallCode}: {oldPrice:N0} đ → {req.GiaThueMoi:N0} đ."
            });
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Cập nhật đơn giá sảnh {hall.HallName} thành công!" });
        }
    }
}

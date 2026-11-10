using HeThongDatTiecCuoi_API.DTOs.PricingPolicy;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPaymentPolicyProvider
{
    IReadOnlyList<PaymentPolicyDto> GetAll();
    PaymentPolicyDto? GetById(int id);
    bool Update(int id, UpdatePaymentPolicyRequest request);
    string BuildContractPaymentTerms();
}

public sealed class PaymentPolicyProvider : IPaymentPolicyProvider
{
    private readonly object _sync = new();
    private readonly List<PaymentPolicyDto> _policies =
    [
        new()
        {
            Id = 1, MaQuyDinh = "POL-PAY-01", TenDieuKhoan = "Đặt cọc Đợt 1 - Giữ chỗ sảnh tiệc",
            PhanLoai = "Đặt cọc & Giữ chỗ", DinhMuc = "20% Tổng dự toán", ThoiHan = "Trong 48 giờ",
            CanCu = "Quy chuẩn giữ chỗ 48h",
            MoTa = "Khách hàng hoàn tất 20% chi phí dự toán để xác lập hợp đồng chính thức. Quá 48 giờ kể từ khi tạo booking nếu chưa nhận cọc, hệ thống tự động hủy giữ chỗ và mở lại lịch sảnh khả dụng.",
            TrangThai = "Hiệu lực"
        },
        new()
        {
            Id = 2, MaQuyDinh = "POL-PAY-02", TenDieuKhoan = "Thanh toán Đợt 2 - Chốt thực đơn & dịch vụ",
            PhanLoai = "Thanh toán đợt", DinhMuc = "50% Tổng giá trị", ThoiHan = "Trước tiệc 7 – 10 ngày",
            CanCu = "Quy trình thanh toán 3 đợt",
            MoTa = "Khách hàng chốt chính thức số lượng bàn phát sinh, danh mục ẩm thực và hoàn tất thanh toán lũy kế đạt tối thiểu 70% giá trị hợp đồng.",
            TrangThai = "Hiệu lực"
        },
        new()
        {
            Id = 3, MaQuyDinh = "POL-PAY-03", TenDieuKhoan = "Quyết toán Đợt 3 - Sau sự kiện",
            PhanLoai = "Quyết toán thanh lý", DinhMuc = "100% Số dư công nợ", ThoiHan = "Ngay khi tan tiệc",
            CanCu = "Thanh lý & quyết toán",
            MoTa = "Đối soát các chi phí phát sinh thực tế (bàn dự phòng, thức uống gọi thêm, phụ phí dịch vụ) và thanh toán toàn bộ số tiền còn lại ngay sau tiệc.",
            TrangThai = "Hiệu lực"
        },
        new()
        {
            Id = 4, MaQuyDinh = "POL-CNC-04", TenDieuKhoan = "Chính sách bồi hoàn & hủy tiệc cưới",
            PhanLoai = "Hủy hợp đồng", DinhMuc = "Phạt 50% - 100% Cọc", ThoiHan = "Theo mốc ngày hủy",
            CanCu = "Bồi thường hợp đồng kinh tế",
            MoTa = "Hủy trước 60 ngày: phạt 50% tiền cọc Đợt 1; Hủy từ 30 đến 59 ngày: phạt 100% tiền cọc Đợt 1; Hủy dưới 30 ngày: bồi hoàn 50% toàn bộ giá trị hợp đồng.",
            TrangThai = "Hiệu lực"
        }
    ];

    public IReadOnlyList<PaymentPolicyDto> GetAll()
    {
        lock (_sync) return _policies.Select(Clone).ToList();
    }

    public PaymentPolicyDto? GetById(int id)
    {
        lock (_sync)
        {
            var item = _policies.FirstOrDefault(policy => policy.Id == id);
            return item is null ? null : Clone(item);
        }
    }

    public bool Update(int id, UpdatePaymentPolicyRequest request)
    {
        lock (_sync)
        {
            var item = _policies.FirstOrDefault(policy => policy.Id == id);
            if (item is null) return false;
            item.TenDieuKhoan = request.TenDieuKhoan.Trim();
            item.DinhMuc = request.DinhMuc.Trim();
            item.ThoiHan = request.ThoiHan.Trim();
            item.MoTa = request.MoTa.Trim();
            return true;
        }
    }

    public string BuildContractPaymentTerms()
    {
        lock (_sync)
        {
            return string.Join(Environment.NewLine + Environment.NewLine,
                _policies.Where(policy => policy.TrangThai == "Hiệu lực")
                    .Select(policy => $"{policy.TenDieuKhoan}: {policy.DinhMuc}; {policy.ThoiHan}. {policy.MoTa}"));
        }
    }

    private static PaymentPolicyDto Clone(PaymentPolicyDto source) => new()
    {
        Id = source.Id,
        MaQuyDinh = source.MaQuyDinh,
        TenDieuKhoan = source.TenDieuKhoan,
        PhanLoai = source.PhanLoai,
        DinhMuc = source.DinhMuc,
        ThoiHan = source.ThoiHan,
        MoTa = source.MoTa,
        CanCu = source.CanCu,
        TrangThai = source.TrangThai
    };
}

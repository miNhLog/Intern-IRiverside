using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<HallSchedule> HallSchedules => Set<HallSchedule>();
    public DbSet<ImageAsset> ImageAssets => Set<ImageAsset>();
    public DbSet<HallManagerAssignment> HallManagerAssignments => Set<HallManagerAssignment>();
    public DbSet<CoordinationAssignment> CoordinationAssignments => Set<CoordinationAssignment>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<WeddingScheduleChange> WeddingScheduleChanges => Set<WeddingScheduleChange>();
    public DbSet<ReviewQrCode> ReviewQrCodes => Set<ReviewQrCode>();
    public DbSet<WeddingReview> WeddingReviews => Set<WeddingReview>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WeddingBooking> WeddingBookings => Set<WeddingBooking>();
    public DbSet<WeddingBookingService> WeddingBookingServices => Set<WeddingBookingService>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RecommendationRequestEntity> RecommendationRequests => Set<RecommendationRequestEntity>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<MenuDish> MenuDishes => Set<MenuDish>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<DataStatus> DataStatuses => Set<DataStatus>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<DecorPackage> DecorPackages { get; set; }
    public DbSet<ServiceItem> ServiceItems { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Status>(entity =>
        {
            entity.ToTable("TrangThai");
            entity.HasKey(x => x.StatusId);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").ValueGeneratedNever();
            entity.Property(x => x.StatusCode).HasColumnName("MaTrangThai").HasColumnType("varchar(40)").HasMaxLength(40).IsRequired();
            entity.Property(x => x.StatusName).HasColumnName("TenTrangThai").HasMaxLength(100).IsRequired();
            entity.Property(x => x.StatusGroup).HasColumnName("NhomTrangThai").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasColumnName("MoTa").HasMaxLength(255);
            entity.HasIndex(x => new { x.StatusGroup, x.StatusCode }).IsUnique();
        });

        modelBuilder.Entity<DataStatus>(entity =>
        {
            entity.ToTable("TrangThaiDuLieu");
            entity.HasKey(x => x.DataStatusId);
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").ValueGeneratedNever();
            entity.Property(x => x.DataStatusCode).HasColumnName("MaTrangThai").HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(x => x.DataStatusName).HasColumnName("TenTrangThai").HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.DataStatusCode).IsUnique();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("TokenDatLaiMatKhau", table =>
                table.HasCheckConstraint("CK_TokenDatLaiMatKhau_HetHan", "[HetHanLuc] > [NgayTao]"));
            entity.HasKey(x => x.PasswordResetTokenId);
            entity.Property(x => x.PasswordResetTokenId).HasColumnName("TokenDatLaiMatKhauID");
            entity.Property(x => x.UserId).HasColumnName("TaiKhoanID");
            entity.Property(x => x.TokenHash).HasColumnName("TokenHash").HasColumnType("varchar(255)").HasMaxLength(255).IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnName("HetHanLuc").HasPrecision(0);
            entity.Property(x => x.UsedAt).HasColumnName("DaDungLuc").HasPrecision(0);
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("VaiTro");
            entity.HasKey(x => x.RoleId);
            entity.Property(x => x.RoleId).HasColumnName("VaiTroID").HasColumnType("tinyint").ValueGeneratedNever();
            entity.Property(x => x.RoleName)
                .HasColumnName("TenVaiTro")
                .HasMaxLength(100)
                .IsRequired();
            entity.HasIndex(x => x.RoleName).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("TaiKhoan");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("TaiKhoanID");
            entity.Property(x => x.RoleId).HasColumnName("VaiTroID").HasColumnType("tinyint");
            entity.Property(x => x.Email).HasColumnName("Email").HasMaxLength(255).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("MatKhauHash").HasMaxLength(255).IsRequired();
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(101);
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("NgayCapNhat").HasPrecision(0);
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.Property(x => x.MustChangePassword).HasColumnName("BatBuocDoiMatKhau").HasDefaultValue(false);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.RoleId, "UX_TaiKhoan_Admin_Active")
                .IsUnique()
                .HasFilter("[VaiTroID] = 1 AND [TrangThaiID] = 101 AND [TrangThaiDuLieuID] = 1");
            entity.HasIndex(x => x.RoleId, "UX_TaiKhoan_Manager_Active")
                .IsUnique()
                .HasFilter("[VaiTroID] = 2 AND [TrangThaiID] = 101 AND [TrangThaiDuLieuID] = 1");
            entity.HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("KhachHang");
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.CustomerId).HasColumnName("KhachHangID");
            entity.Property(x => x.CustomerCode).HasColumnName("MaKhachHang").HasMaxLength(20).IsRequired();
            entity.Property(x => x.UserId).HasColumnName("TaiKhoanID");
            entity.Property(x => x.FullName).HasColumnName("HoTen").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PhoneNumber).HasColumnName("SoDienThoai").HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasColumnName("Email").HasMaxLength(255);
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => x.CustomerCode).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("[TaiKhoanID] IS NOT NULL");
            entity.HasOne(x => x.User)
                .WithOne(x => x.Customer)
                .HasForeignKey<Customer>(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("NhanVien");
            entity.HasKey(x => x.EmployeeId);
            entity.Property(x => x.EmployeeId).HasColumnName("NhanVienID");
            entity.Property(x => x.UserId).HasColumnName("TaiKhoanID");
            entity.Property(x => x.EmployeeCode).HasColumnName("MaNhanVien").HasMaxLength(20).IsRequired();
            entity.Property(x => x.FullName).HasColumnName("HoTen").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PhoneNumber).HasColumnName("SoDienThoai").HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.EmployeeCode).IsUnique();
            entity.HasOne(x => x.User)
                .WithOne(x => x.Employee)
                .HasForeignKey<Employee>(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Hall>(entity =>
        {
            entity.ToTable("SanhTiec", table =>
            {
                table.HasCheckConstraint("CK_SanhTiec_SucChuaToiThieu", "[SucChuaToiThieu] IS NULL OR [SucChuaToiThieu] > 0");
                table.HasCheckConstraint("CK_SanhTiec_SucChuaToiDa", "[SucChuaToiDa] > 0");
                table.HasCheckConstraint("CK_SanhTiec_KhoangSucChua", "[SucChuaToiThieu] IS NULL OR [SucChuaToiThieu] <= [SucChuaToiDa]");
                table.HasCheckConstraint("CK_SanhTiec_GiaThue", "[GiaThue] >= 0");
            });
            entity.HasKey(x => x.HallId);
            entity.Property(x => x.HallId).HasColumnName("SanhTiecID");
            entity.Property(x => x.HallCode).HasColumnName("MaSanh").HasMaxLength(20).IsRequired();
            entity.Property(x => x.HallName).HasColumnName("TenSanh").HasMaxLength(150).IsRequired();
            entity.Property(x => x.MinimumCapacity).HasColumnName("SucChuaToiThieu");
            entity.Property(x => x.MaximumCapacity).HasColumnName("SucChuaToiDa");
            entity.Property(x => x.RentalPrice).HasColumnName("GiaThue").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.Description).HasColumnName("MoTa").HasMaxLength(1000);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(201);
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => x.HallCode).IsUnique();
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<HallSchedule>(entity =>
        {
            entity.ToTable("LichSanh", table =>
                table.HasCheckConstraint("CK_LichSanh_CaToChuc", "[CaToChuc] IN (N'Ca trưa', N'Ca tối')"));

            entity.HasKey(x => x.HallScheduleId);

            entity.Property(x => x.HallScheduleId)
                .HasColumnName("LichSanhID");

            entity.Property(x => x.HallId)
                .HasColumnName("SanhTiecID");

            entity.Property(x => x.Date)
                .HasColumnName("Ngay")
                .HasColumnType("date");

            entity.Property(x => x.Shift)
                .HasColumnName("CaToChuc")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.StatusId)
                .HasColumnName("TrangThaiID")
                .HasDefaultValue(301);

            entity.Property(x => x.DataStatusId)
                .HasColumnName("TrangThaiDuLieuID")
                .HasDefaultValue((byte)1);

            entity.HasIndex(x => new
            {
                x.HallId,
                x.Date,
                x.Shift
            }).IsUnique()
              .HasDatabaseName("UX_LichSanh_Sanh_Ngay_Ca_EXISTING")
              .HasFilter("[TrangThaiDuLieuID] = 1");

            entity.HasOne(x => x.Hall)
                .WithMany(x => x.Schedules)
                .HasForeignKey(x => x.HallId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ImageAsset>(entity =>
        {
            entity.ToTable("HinhAnh", table =>
            {
                table.HasCheckConstraint(
                    "CK_HinhAnh_MotDoiTuong",
                    "([SanhTiecID] IS NOT NULL AND [GoiTrangTriID] IS NULL) OR ([SanhTiecID] IS NULL AND [GoiTrangTriID] IS NOT NULL)");
                table.HasCheckConstraint("CK_HinhAnh_SoThuTu", "[SoThuTu] > 0");
            });
            entity.HasKey(x => x.ImageId);
            entity.Property(x => x.ImageId).HasColumnName("HinhAnhID");
            entity.Property(x => x.HallId).HasColumnName("SanhTiecID");
            entity.Property(x => x.DecorationPackageId).HasColumnName("GoiTrangTriID");
            entity.Property(x => x.ImagePath).HasColumnName("DuongDanAnh").HasMaxLength(500).IsRequired();
            entity.Property(x => x.IsPrimary).HasColumnName("LaAnhDaiDien").HasDefaultValue(false);
            entity.Property(x => x.SortOrder).HasColumnName("SoThuTu").HasDefaultValue(1);
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => x.HallId, "UX_HinhAnh_Sanh_AnhDaiDien")
                .IsUnique()
                .HasFilter("[SanhTiecID] IS NOT NULL AND [LaAnhDaiDien] = 1 AND [TrangThaiDuLieuID] = 1");
            entity.HasIndex(x => x.DecorationPackageId, "UX_HinhAnh_GoiTrangTri_AnhDaiDien")
                .IsUnique()
                .HasFilter("[GoiTrangTriID] IS NOT NULL AND [LaAnhDaiDien] = 1 AND [TrangThaiDuLieuID] = 1");
            entity.HasOne(x => x.Hall)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.HallId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DecorationPackage)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.DecorationPackageId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<HallManagerAssignment>(entity =>
        {
            entity.ToTable("PhanCongQuanLySanh", table =>
                table.HasCheckConstraint(
                    "CK_PhanCongQuanLySanh_ThoiGian",
                    "[DenNgay] IS NULL OR [DenNgay] >= [TuNgay]"));
            entity.HasKey(x => x.HallManagerAssignmentId);
            entity.Property(x => x.HallManagerAssignmentId).HasColumnName("PhanCongQuanLySanhID");
            entity.Property(x => x.HallId).HasColumnName("SanhTiecID");
            entity.Property(x => x.HallManagerEmployeeId).HasColumnName("NhanVienQuanLySanhID");
            entity.Property(x => x.FromDate).HasColumnName("TuNgay").HasColumnType("date")
                .HasDefaultValueSql("CONVERT(date, SYSDATETIME())");
            entity.Property(x => x.ToDate).HasColumnName("DenNgay").HasColumnType("date");
            entity.HasIndex(x => x.HallId, "UX_PhanCongQuanLySanh_HienTai")
                .IsUnique()
                .HasFilter("[DenNgay] IS NULL");
            entity.HasOne(x => x.Hall)
                .WithMany(x => x.HallManagerAssignments)
                .HasForeignKey(x => x.HallId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.HallManager)
                .WithMany(x => x.HallManagerAssignments)
                .HasForeignKey(x => x.HallManagerEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoordinationAssignment>(entity =>
        {
            entity.ToTable("PhanCongDieuPhoi");
            entity.HasKey(x => x.CoordinationAssignmentId);
            entity.Property(x => x.CoordinationAssignmentId).HasColumnName("PhanCongID");
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.CoordinatorEmployeeId).HasColumnName("NhanVienDieuPhoiID");
            entity.Property(x => x.AssignedByHallManagerEmployeeId).HasColumnName("QuanLySanhPhanCongID");
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(1101);
            entity.Property(x => x.AssignedAt).HasColumnName("NgayPhanCong").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(x => x.BookingId, "UX_PhanCongDieuPhoi_DatTiec_ASSIGNED")
                .IsUnique()
                .HasFilter("[TrangThaiID] = 1101");
            entity.HasOne(x => x.Booking)
                .WithMany(x => x.CoordinationAssignments)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Coordinator)
                .WithMany(x => x.CoordinationAssignmentsAsCoordinator)
                .HasForeignKey(x => x.CoordinatorEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.AssignedByHallManager)
                .WithMany(x => x.CoordinationAssignmentsAsHallManager)
                .HasForeignKey(x => x.AssignedByHallManagerEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("SuCoTiec", table =>
            {
                table.HasCheckConstraint(
                    "CK_SuCoTiec_MucDo",
                    "[MucDo] IN (N'Thấp', N'Bình thường', N'Nghiêm trọng')");
                table.HasCheckConstraint(
                    "CK_SuCoTiec_ThoiGianXuLy",
                    "[ThoiGianXuLy] IS NULL OR [ThoiGianXuLy] >= [ThoiGianPhatSinh]");
            });
            entity.HasKey(x => x.IncidentId);
            entity.Property(x => x.IncidentId).HasColumnName("SuCoID");
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.ReportedByEmployeeId).HasColumnName("NhanVienBaoCaoID");
            entity.Property(x => x.IncidentType).HasColumnName("LoaiSuCo").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasColumnName("MoTa").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Severity).HasColumnName("MucDo").HasMaxLength(20)
                .HasDefaultValue("Bình thường").IsRequired();
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(1201);
            entity.Property(x => x.Resolution).HasColumnName("HuongXuLy").HasColumnType("nvarchar(max)");
            entity.Property(x => x.OccurredAt).HasColumnName("ThoiGianPhatSinh").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.ResolvedAt).HasColumnName("ThoiGianXuLy").HasPrecision(0);
            entity.HasOne(x => x.Booking)
                .WithMany(x => x.Incidents)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.ReportedByEmployee)
                .WithMany(x => x.ReportedIncidents)
                .HasForeignKey(x => x.ReportedByEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WeddingScheduleChange>(entity =>
        {
            entity.ToTable("ThayDoiLichTiec", table =>
            {
                table.HasCheckConstraint(
                    "CK_ThayDoiLichTiec_KhacLich",
                    "[LichSanhCuID] <> [LichSanhMoiID]");
                table.HasCheckConstraint(
                    "CK_ThayDoiLichTiec_NgayXuLy",
                    "[NgayXuLy] IS NULL OR [NgayXuLy] >= [NgayYeuCau]");
            });
            entity.HasKey(x => x.ScheduleChangeId);
            entity.Property(x => x.ScheduleChangeId).HasColumnName("ThayDoiLichID");
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.OldHallScheduleId).HasColumnName("LichSanhCuID");
            entity.Property(x => x.NewHallScheduleId).HasColumnName("LichSanhMoiID");
            entity.Property(x => x.Reason).HasColumnName("LyDo").HasMaxLength(500);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(1301);
            entity.Property(x => x.RequestedAt).HasColumnName("NgayYeuCau").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.ProcessedAt).HasColumnName("NgayXuLy").HasPrecision(0);
            entity.HasOne(x => x.Booking)
                .WithMany(x => x.ScheduleChanges)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.OldHallSchedule)
                .WithMany(x => x.ScheduleChangesAsOldSchedule)
                .HasForeignKey(x => x.OldHallScheduleId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.NewHallSchedule)
                .WithMany(x => x.ScheduleChangesAsNewSchedule)
                .HasForeignKey(x => x.NewHallScheduleId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReviewQrCode>(entity =>
        {
            entity.ToTable("MaQRDanhGia", table =>
                table.HasCheckConstraint(
                    "CK_MaQRDanhGia_NgayHetHan",
                    "[NgayHetHan] > [NgayTao]"));
            entity.HasKey(x => x.ReviewQrCodeId);
            entity.Property(x => x.ReviewQrCodeId).HasColumnName("MaQRDanhGiaID");
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.QrCode).HasColumnName("MaQR").HasMaxLength(150).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.ExpiresAt).HasColumnName("NgayHetHan").HasPrecision(0);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(1401);
            entity.HasIndex(x => x.BookingId, "UQ_MaQRDanhGia_DatTiec").IsUnique();
            entity.HasIndex(x => x.QrCode, "UQ_MaQRDanhGia_MaQR").IsUnique();
            entity.HasOne(x => x.Booking)
                .WithOne(x => x.ReviewQrCode)
                .HasForeignKey<ReviewQrCode>(x => x.BookingId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WeddingReview>(entity =>
        {
            entity.ToTable("DanhGia", table =>
            {
                table.HasCheckConstraint(
                    "CK_DanhGia_LoaiNguoiDanhGia",
                    "[LoaiNguoiDanhGia] IN (N'Khách mời', N'Chủ tiệc')");
                table.HasCheckConstraint("CK_DanhGia_DiemSanh", "[DiemSanh] IS NULL OR [DiemSanh] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_DanhGia_DiemMonAn", "[DiemMonAn] IS NULL OR [DiemMonAn] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_DanhGia_DiemPhucVu", "[DiemPhucVu] IS NULL OR [DiemPhucVu] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_DanhGia_DiemAmThanh", "[DiemAmThanh] IS NULL OR [DiemAmThanh] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_DanhGia_DiemAnhSang", "[DiemAnhSang] IS NULL OR [DiemAnhSang] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_DanhGia_DiemTongThe", "[DiemTongThe] BETWEEN 1 AND 5");
            });
            entity.HasKey(x => x.ReviewId);
            entity.Property(x => x.ReviewId).HasColumnName("DanhGiaID");
            entity.Property(x => x.ReviewQrCodeId).HasColumnName("MaQRDanhGiaID");
            entity.Property(x => x.ReviewerType).HasColumnName("LoaiNguoiDanhGia").HasMaxLength(50).IsRequired();
            entity.Property(x => x.HallScore).HasColumnName("DiemSanh");
            entity.Property(x => x.FoodScore).HasColumnName("DiemMonAn");
            entity.Property(x => x.ServiceScore).HasColumnName("DiemPhucVu");
            entity.Property(x => x.SoundScore).HasColumnName("DiemAmThanh");
            entity.Property(x => x.LightingScore).HasColumnName("DiemAnhSang");
            entity.Property(x => x.OverallScore).HasColumnName("DiemTongThe");
            entity.Property(x => x.Comment).HasColumnName("BinhLuan").HasColumnType("nvarchar(max)");
            entity.Property(x => x.ReviewedAt).HasColumnName("NgayDanhGia").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.HasOne(x => x.ReviewQrCode)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.ReviewQrCodeId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("NhatKyThaoTac");
            entity.HasKey(x => x.AuditLogId);
            entity.Property(x => x.AuditLogId).HasColumnName("NhatKyThaoTacID");
            entity.Property(x => x.UserId).HasColumnName("TaiKhoanID");
            entity.Property(x => x.Action).HasColumnName("HanhDong")
                .HasColumnType("varchar(30)").HasMaxLength(30).IsRequired();
            entity.Property(x => x.EntityName).HasColumnName("DoiTuong").HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasColumnName("DoiTuongID");
            entity.Property(x => x.OldData).HasColumnName("DuLieuCu").HasColumnType("nvarchar(max)");
            entity.Property(x => x.NewData).HasColumnName("DuLieuMoi").HasColumnType("nvarchar(max)");
            entity.Property(x => x.Timestamp).HasColumnName("ThoiGian").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.Notes).HasColumnName("GhiChu").HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<DecorPackage>(entity =>
        {
            entity.ToTable("GoiTrangTri", table =>
                table.HasCheckConstraint("CK_GoiTrangTri_Gia", "[Gia] >= 0"));
            entity.HasKey(x => x.GoiTrangTriID);
            entity.Property(x => x.GoiTrangTriID).HasColumnName("GoiTrangTriID");
            entity.Property(x => x.MaGoi).HasColumnName("MaGoi").HasMaxLength(20).IsRequired();
            entity.Property(x => x.TenGoi).HasColumnName("TenGoi").HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhongCach).HasColumnName("PhongCach").HasMaxLength(100).IsRequired();
            entity.Property(x => x.MoTa).HasColumnName("MoTa").HasMaxLength(1000);
            entity.Property(x => x.Gia).HasColumnName("Gia").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(601);
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => x.MaGoi).IsUnique();
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus).WithMany().HasForeignKey(x => x.DataStatusId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ServiceItem>(entity =>
        {
            entity.ToTable("DichVu", table =>
                table.HasCheckConstraint("CK_DichVu_Gia", "[Gia] >= 0"));
            entity.HasKey(x => x.DichVuID);
            entity.Property(x => x.DichVuID).HasColumnName("DichVuID");
            entity.Property(x => x.MaDichVu).HasColumnName("MaDichVu").HasMaxLength(20).IsRequired();
            entity.Property(x => x.TenDichVu).HasColumnName("TenDichVu").HasMaxLength(200).IsRequired();
            entity.Property(x => x.LoaiDichVu).HasColumnName("LoaiDichVu").HasMaxLength(100);
            entity.Property(x => x.MoTa).HasColumnName("MoTa").HasMaxLength(1000);
            entity.Property(x => x.Gia).HasColumnName("Gia").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(701);
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => x.MaDichVu).IsUnique();
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus).WithMany().HasForeignKey(x => x.DataStatusId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WeddingBooking>(entity =>
        {
            entity.ToTable("DatTiec", table =>
            {
                table.HasCheckConstraint("CK_DatTiec_SoLuongKhach", "[SoLuongKhach] > 0");
                table.HasCheckConstraint("CK_DatTiec_GiaThucDonChot", "[GiaThucDonChot] IS NULL OR [GiaThucDonChot] >= 0");
                table.HasCheckConstraint("CK_DatTiec_GiaTrangTriChot", "[GiaTrangTriChot] IS NULL OR [GiaTrangTriChot] >= 0");
                table.HasCheckConstraint("CK_DatTiec_GiaSanhChot", "[GiaSanhChot] IS NULL OR [GiaSanhChot] >= 0");
                table.HasCheckConstraint("CK_DatTiec_TongTienDuKien", "[TongTienDuKien] IS NULL OR [TongTienDuKien] >= 0");
            });
            entity.HasKey(x => x.BookingId);
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.BookingCode).HasColumnName("MaDatTiec").HasMaxLength(50).IsRequired();
            entity.Property(x => x.CustomerId).HasColumnName("KhachHangID");
            entity.Property(x => x.HallScheduleId).HasColumnName("LichSanhID");
            entity.Property(x => x.MenuId).HasColumnName("ThucDonID");
            entity.Property(x => x.DecorationPackageId).HasColumnName("GoiTrangTriID");
            entity.Property(x => x.GuestCount).HasColumnName("SoLuongKhach").IsRequired();
            entity.Property(x => x.FinalMenuPrice).HasColumnName("GiaThucDonChot").HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinalDecorationPrice).HasColumnName("GiaTrangTriChot").HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinalHallPrice).HasColumnName("GiaSanhChot").HasColumnType("decimal(18,2)");
            entity.Property(x => x.EstimatedTotal).HasColumnName("TongTienDuKien").HasColumnType("decimal(18,2)");
            entity.Property(x => x.SpecialRequests).HasColumnName("YeuCauDacBiet").HasColumnType("nvarchar(max)");
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(801);
            entity.Property(x => x.CancellationReason).HasColumnName("LyDoHuy").HasMaxLength(500);
            entity.Property(x => x.BookedAt).HasColumnName("NgayDat").HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(x => x.BookingCode).IsUnique();
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.HallSchedule).WithMany().HasForeignKey(x => x.HallScheduleId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Menu).WithMany().HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DecorationPackage).WithMany().HasForeignKey(x => x.DecorationPackageId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WeddingBookingService>(entity =>
        {
            entity.ToTable("DatTiec_DichVu", table =>
            {
                table.HasCheckConstraint("CK_DatTiec_DichVu_SoLuong", "[SoLuong] > 0");
                table.HasCheckConstraint("CK_DatTiec_DichVu_DonGiaChot", "[DonGiaChot] >= 0");
            });
            entity.HasKey(x => x.BookingServiceId);
            entity.Property(x => x.BookingServiceId).HasColumnName("DatTiecDichVuID");
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.ServiceId).HasColumnName("DichVuID");
            entity.Property(x => x.Quantity).HasColumnName("SoLuong").HasDefaultValue(1);
            entity.Property(x => x.FinalUnitPrice).HasColumnName("DonGiaChot").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.DataStatusId).HasColumnName("TrangThaiDuLieuID").HasDefaultValue((byte)1);
            entity.HasIndex(x => new { x.BookingId, x.ServiceId }).IsUnique();
            entity.HasOne(x => x.WeddingBooking).WithMany(x => x.BookingServices).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.DataStatus).WithMany().HasForeignKey(x => x.DataStatusId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("HopDong", table =>
                table.HasCheckConstraint("CK_HopDong_TongGiaTri", "[TongGiaTri] >= 0"));
            entity.HasKey(x => x.ContractId);
            entity.Property(x => x.ContractId).HasColumnName("HopDongID");
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.ContractCode).HasColumnName("MaHopDong").HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedDate).HasColumnName("NgayLap").HasColumnType("date")
                .HasDefaultValueSql("CONVERT(date, SYSDATETIME())");
            entity.Property(x => x.TotalValue).HasColumnName("TongGiaTri").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.ContractContent).HasColumnName("NoiDungHopDong").HasColumnType("nvarchar(max)");
            entity.Property(x => x.PaymentTerms).HasColumnName("DieuKhoanThanhToan").HasColumnType("nvarchar(max)");
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(901);
            entity.HasIndex(x => x.BookingId).IsUnique();
            entity.HasIndex(x => x.ContractCode).IsUnique();
            entity.HasOne(x => x.Booking).WithOne(x => x.Contract)
                .HasForeignKey<Contract>(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("ThanhToan", table =>
            {
                table.HasCheckConstraint(
                    "CK_ThanhToan_Loai",
                    "[LoaiThanhToan] IN (N'Đặt cọc', N'Thanh toán đợt', N'Quyết toán')");
                table.HasCheckConstraint("CK_ThanhToan_SoTien", "[SoTien] > 0");
            });
            entity.HasKey(x => x.PaymentId);
            entity.Property(x => x.PaymentId).HasColumnName("ThanhToanID");
            entity.Property(x => x.ContractId).HasColumnName("HopDongID");
            entity.Property(x => x.PaymentType).HasColumnName("LoaiThanhToan").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Amount).HasColumnName("SoTien").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.PaymentDate).HasColumnName("NgayThanhToan").HasPrecision(0)
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.PaymentMethod).HasColumnName("PhuongThuc").HasMaxLength(50);
            entity.Property(x => x.TransactionCode).HasColumnName("MaGiaoDich").HasMaxLength(100);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID").HasDefaultValue(1001);
            entity.HasOne(x => x.Contract).WithMany(x => x.Payments)
                .HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RecommendationRequestEntity>(entity =>
        {
            entity.ToTable("YeuCauKhuyenNghi", table =>
            {
                table.HasCheckConstraint("CK_YeuCauKhuyenNghi_NganSach", "[NganSachDuKien] >= 0");
                table.HasCheckConstraint("CK_YeuCauKhuyenNghi_SoLuongKhach", "[SoLuongKhach] > 0");
                table.HasCheckConstraint("CK_YeuCauKhuyenNghi_Ca", "[CaToChucMongMuon] IN (N'Ca trưa', N'Ca tối')");
            });
            entity.HasKey(x => x.RecommendationRequestId);
            entity.Property(x => x.RecommendationRequestId).HasColumnName("YeuCauKhuyenNghiID");
            entity.Property(x => x.CustomerId).HasColumnName("KhachHangID");
            entity.Property(x => x.ExpectedBudget).HasColumnName("NganSachDuKien").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.GuestCount).HasColumnName("SoLuongKhach").IsRequired();
            entity.Property(x => x.DesiredDate).HasColumnName("NgayToChucMongMuon").HasColumnType("date").IsRequired();
            entity.Property(x => x.DesiredShift).HasColumnName("CaToChucMongMuon").HasMaxLength(20).IsRequired();
            entity.Property(x => x.DesiredStyle).HasColumnName("PhongCachMongMuon").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ServiceNeeds).HasColumnName("NhuCauDichVu").HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.ToTable("ThucDon", table =>
            {
                table.HasCheckConstraint("CK_ThucDon_LoaiThucDon", "[LoaiThucDon] IN ('STANDARD', 'CUSTOM')");
                table.HasCheckConstraint(
                    "CK_ThucDon_KhachHang",
                    "([LoaiThucDon] = 'STANDARD' AND [KhachHangID] IS NULL) OR ([LoaiThucDon] = 'CUSTOM' AND [KhachHangID] IS NOT NULL)");
                table.HasCheckConstraint("CK_ThucDon_GiaMoiBan", "[GiaMoiBan] >= 0");
            });

            entity.HasKey(x => x.MenuId);

            entity.Property(x => x.MenuId)
                .HasColumnName("ThucDonID");

            entity.Property(x => x.MenuCode)
                .HasColumnName("MaThucDon")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.MenuName)
                .HasColumnName("TenThucDon")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.MenuType)
                .HasColumnName("LoaiThucDon")
                .HasColumnType("varchar(20)")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.CustomerId)
                .HasColumnName("KhachHangID");

            entity.Property(x => x.Description)
                .HasColumnName("MoTa")
                .HasMaxLength(1000);

            entity.Property(x => x.PricePerTable)
                .HasColumnName("GiaMoiBan")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.StatusId)
                .HasColumnName("TrangThaiID")
                .HasDefaultValue(401);

            entity.Property(x => x.DataStatusId)
                .HasColumnName("TrangThaiDuLieuID")
                .HasDefaultValue((byte)1);

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Menus)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.MenuCode)
                .IsUnique();
        });


        modelBuilder.Entity<Dish>(entity =>
        {
            entity.ToTable("MonAn", table =>
                table.HasCheckConstraint("CK_MonAn_GiaMon", "[GiaMon] >= 0"));

            entity.HasKey(x => x.DishId);

            entity.Property(x => x.DishId)
                .HasColumnName("MonAnID");

            entity.Property(x => x.DishCode)
                .HasColumnName("MaMon")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.DishName)
                .HasColumnName("TenMon")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Category)
                .HasColumnName("NhomMon")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Price)
                .HasColumnName("GiaMon")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.ImageUrl)
                .HasColumnName("HinhAnh")
                .HasMaxLength(500);

            entity.Property(x => x.StatusId)
                .HasColumnName("TrangThaiID")
                .HasDefaultValue(501);

            entity.Property(x => x.DataStatusId)
                .HasColumnName("TrangThaiDuLieuID")
                .HasDefaultValue((byte)1);

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.DishCode)
                .IsUnique();
        });


        modelBuilder.Entity<MenuDish>(entity =>
        {
            entity.ToTable("ChiTietThucDon", table =>
                table.HasCheckConstraint("CK_ChiTietThucDon_SoThuTu", "[SoThuTu] > 0"));

            entity.HasKey(x => x.MenuDishId);

            entity.Property(x => x.MenuDishId)
                .HasColumnName("ChiTietThucDonID");

            entity.Property(x => x.MenuId)
                .HasColumnName("ThucDonID");

            entity.Property(x => x.DishId)
                .HasColumnName("MonAnID");

            entity.Property(x => x.SortOrder)
                .HasColumnName("SoThuTu")
                .IsRequired();

            entity.Property(x => x.DataStatusId)
                .HasColumnName("TrangThaiDuLieuID")
                .HasDefaultValue((byte)1);

            entity.HasIndex(x => new
            {
                x.MenuId,
                x.DishId
            })
            .IsUnique();

            entity.HasIndex(x => new
            {
                x.MenuId,
                x.SortOrder
            })
            .IsUnique()
            .HasDatabaseName("UX_ChiTietThucDon_ThucDon_SoThuTu_EXISTING")
            .HasFilter("[TrangThaiDuLieuID] = 1");

            entity.HasOne(x => x.Menu)
                .WithMany(x => x.MenuDishes)
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Dish)
                .WithMany(x => x.MenuDishes)
                .HasForeignKey(x => x.DishId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.DataStatus)
                .WithMany()
                .HasForeignKey(x => x.DataStatusId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

}

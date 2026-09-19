using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SanhTiec> SanhTiec => Set<SanhTiec>();
    public DbSet<VaiTro> VaiTro => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDung => Set<NguoiDung>();
    public DbSet<KhachHang> KhachHang => Set<KhachHang>();
    public DbSet<NhanVien> NhanVien => Set<NhanVien>();
    public DbSet<LichSanh> LichSanh => Set<LichSanh>();
    public DbSet<DatTiec> DatTiec => Set<DatTiec>();
    public DbSet<ThucDon> ThucDon => Set<ThucDon>();
    public DbSet<GoiTrangTri> GoiTrangTri => Set<GoiTrangTri>();
    public DbSet<DichVu> DichVu => Set<DichVu>();
    public DbSet<DatTiecDichVu> DatTiecDichVu => Set<DatTiecDichVu>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.ToTable("VaiTro");
            entity.HasKey(x => x.VaiTroID);
            entity.Property(x => x.TenVaiTro).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.TenVaiTro).IsUnique();
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.ToTable("NguoiDung");
            entity.HasKey(x => x.NguoiDungID);
            entity.Property(x => x.Email).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MatKhauHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NgayTao).HasPrecision(0);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne(x => x.VaiTro)
                .WithMany(x => x.NguoiDungs)
                .HasForeignKey(x => x.VaiTroID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.ToTable("KhachHang");
            entity.HasKey(x => x.KhachHangID);
            entity.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SoDienThoai).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.NguoiDungID).IsUnique().HasFilter("[NguoiDungID] IS NOT NULL");
            entity.HasIndex(x => x.SoDienThoai).IsUnique();
            entity.HasOne(x => x.NguoiDung)
                .WithOne(x => x.KhachHang)
                .HasForeignKey<KhachHang>(x => x.NguoiDungID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.ToTable("NhanVien");
            entity.HasKey(x => x.NhanVienID);
            entity.Property(x => x.MaNhanVien).HasMaxLength(50).IsRequired();
            entity.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SoDienThoai).HasMaxLength(20);
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.NguoiDungID).IsUnique();
            entity.HasIndex(x => x.MaNhanVien).IsUnique();
            entity.HasIndex(x => x.SoDienThoai).IsUnique().HasFilter("[SoDienThoai] IS NOT NULL");
            entity.HasOne(x => x.NguoiDung)
                .WithOne(x => x.NhanVien)
                .HasForeignKey<NhanVien>(x => x.NguoiDungID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SanhTiec>(entity =>
        {
            entity.ToTable("SanhTiec");
            entity.HasKey(x => x.SanhTiecID);
            entity.Property(x => x.MaSanh).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenSanh).HasMaxLength(150).IsRequired();
            entity.Property(x => x.GiaThue).HasColumnType("decimal(18,2)");
            entity.Property(x => x.HinhAnh).HasMaxLength(500);
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<LichSanh>(entity =>
        {
            entity.ToTable("LichSanh");

            entity.HasKey(x => x.LichSanhID);

            entity.Property(x => x.Ngay)
                .HasColumnType("date");

            entity.Property(x => x.CaToChuc)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.GhiChu)
                .HasMaxLength(500);

            entity.HasIndex(x => new
            {
                x.SanhTiecID,
                x.Ngay,
                x.CaToChuc
            }).IsUnique();

            entity.HasOne(x => x.SanhTiec)
                .WithMany()
                .HasForeignKey(x => x.SanhTiecID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ThucDon>(entity =>
        {
            entity.ToTable("ThucDon", table =>
            {
                table.HasCheckConstraint("CK_ThucDon_Gia", "[GiaMoiBan] >= 0");
                table.HasCheckConstraint(
                    "CK_ThucDon_TrangThai",
                    "[TrangThai] IN (N'Áp dụng', N'Ngừng áp dụng')");
            });

            entity.HasKey(x => x.ThucDonID);
            entity.Property(x => x.MaThucDon)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.TenThucDon)
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(x => x.MoTa)
                .HasColumnType("nvarchar(max)");
            entity.Property(x => x.GiaMoiBan)
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Áp dụng");

            entity.HasIndex(x => x.MaThucDon)
                .IsUnique()
                .HasDatabaseName("UQ_ThucDon_MaThucDon");
        });

        modelBuilder.Entity<DatTiec>(entity =>
        {
            entity.ToTable("DatTiec");

            entity.HasKey(x => x.DatTiecID);

            entity.Property(x => x.MaDatTiec)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.MaDatTiec)
                .IsUnique();

            entity.Property(x => x.NganSachDuKien)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.PhongCachMongMuon)
                .HasMaxLength(100);

            entity.Property(x => x.GiaThucDonChot)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.GiaTrangTriChot)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.GiaSanhChot)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TongTienDuKien)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.LyDoHuy)
                .HasMaxLength(500);

            entity.Property(x => x.NgayDat)
                .HasPrecision(0);

            entity.Property(x => x.NgayCapNhat)
                .HasPrecision(0);

            entity.HasOne(x => x.KhachHang)
                .WithMany()
                .HasForeignKey(x => x.KhachHangID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LichSanh)
                .WithMany()
                .HasForeignKey(x => x.LichSanhID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ThucDon>()
                .WithMany()
                .HasForeignKey(x => x.ThucDonID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.GoiTrangTri)
                .WithMany(x => x.DatTiecs)
                .HasForeignKey(x => x.GoiTrangTriID)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GoiTrangTri>(entity =>
        {
            entity.ToTable("GoiTrangTri", table =>
            {
                table.HasCheckConstraint("CK_GoiTrangTri_Gia", "[Gia] >= 0");
                table.HasCheckConstraint(
                    "CK_GoiTrangTri_TrangThai",
                    "[TrangThai] IN (N'Áp dụng', N'Ngừng áp dụng')");
            });

            entity.HasKey(x => x.GoiTrangTriID);
            entity.Property(x => x.MaGoi).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenGoi).HasMaxLength(150).IsRequired();
            entity.Property(x => x.PhongCach).HasMaxLength(100);
            entity.Property(x => x.MoTa).HasColumnType("nvarchar(max)");
            entity.Property(x => x.HinhAnh).HasMaxLength(500);
            entity.Property(x => x.Gia)
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Áp dụng");

            entity.HasIndex(x => x.MaGoi)
                .IsUnique()
                .HasDatabaseName("UQ_GoiTrangTri_MaGoi");
        });

        modelBuilder.Entity<DichVu>(entity =>
        {
            entity.ToTable("DichVu", table =>
            {
                table.HasCheckConstraint("CK_DichVu_Gia", "[Gia] >= 0");
                table.HasCheckConstraint(
                    "CK_DichVu_TrangThai",
                    "[TrangThai] IN (N'Áp dụng', N'Ngừng áp dụng')");
            });

            entity.HasKey(x => x.DichVuID);
            entity.Property(x => x.MaDichVu).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenDichVu).HasMaxLength(150).IsRequired();
            entity.Property(x => x.LoaiDichVu).HasMaxLength(100);
            entity.Property(x => x.MoTa).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Gia)
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.HinhAnh).HasMaxLength(500);
            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Áp dụng");

            entity.HasIndex(x => x.MaDichVu)
                .IsUnique()
                .HasDatabaseName("UQ_DichVu_MaDichVu");
        });

        modelBuilder.Entity<DatTiecDichVu>(entity =>
        {
            entity.ToTable("DatTiec_DichVu", table =>
            {
                table.HasCheckConstraint("CK_DatTiecDichVu_SoLuong", "[SoLuong] > 0");
                table.HasCheckConstraint("CK_DatTiecDichVu_DonGia", "[DonGiaChot] >= 0");
            });

            entity.HasKey(x => x.DatTiecDichVuID);

            entity.Property(x => x.SoLuong)
                .IsRequired()
                .HasDefaultValue(1);
            entity.Property(x => x.DonGiaChot)
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(500);

            entity.HasIndex(x => new { x.DatTiecID, x.DichVuID })
                .IsUnique()
                .HasDatabaseName("UQ_DatTiecDichVu_DatTiec_DichVu");

            entity.HasOne(x => x.DatTiec)
                .WithMany(x => x.DatTiecDichVus)
                .HasForeignKey(x => x.DatTiecID)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.DichVu)
                .WithMany(x => x.DatTiecDichVus)
                .HasForeignKey(x => x.DichVuID)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}

USE master;
GO
CREATE DATABASE IRiversidePalaceDB;
GO

USE IRiversidePalaceDB;
GO

-- 01. VaiTro
CREATE TABLE VaiTro (
    VaiTroID TINYINT NOT NULL,
    TenVaiTro NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_VaiTro PRIMARY KEY (VaiTroID),
    CONSTRAINT UQ_VaiTro_TenVaiTro UNIQUE (TenVaiTro)
);
GO

-- 02. TrangThai
CREATE TABLE TrangThai (
    TrangThaiID INT NOT NULL,
    MaTrangThai VARCHAR(40) NOT NULL,
    TenTrangThai NVARCHAR(100) NOT NULL,
    NhomTrangThai VARCHAR(50) NOT NULL,
    MoTa NVARCHAR(255) NULL,
    CONSTRAINT PK_TrangThai PRIMARY KEY (TrangThaiID),
    CONSTRAINT UQ_TrangThai_Nhom_Ma UNIQUE (NhomTrangThai, MaTrangThai)
);
GO

-- 03. TrangThaiDuLieu
CREATE TABLE TrangThaiDuLieu (
    TrangThaiDuLieuID TINYINT NOT NULL,
    MaTrangThai VARCHAR(20) NOT NULL,
    TenTrangThai NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_TrangThaiDuLieu PRIMARY KEY (TrangThaiDuLieuID),
    CONSTRAINT UQ_TrangThaiDuLieu_Ma UNIQUE (MaTrangThai)
);
GO



-- 04. TaiKhoan
CREATE TABLE TaiKhoan (
    TaiKhoanID INT IDENTITY(1,1) NOT NULL,
    VaiTroID TINYINT NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    MatKhauHash NVARCHAR(255) NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_TaiKhoan_TrangThai DEFAULT 101,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_TaiKhoan_NgayTao DEFAULT SYSDATETIME(),
    NgayCapNhat DATETIME2(0) NULL,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_TaiKhoan_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_TaiKhoan PRIMARY KEY (TaiKhoanID),
    CONSTRAINT UQ_TaiKhoan_Email UNIQUE (Email),
    CONSTRAINT FK_TaiKhoan_VaiTro FOREIGN KEY (VaiTroID) REFERENCES VaiTro(VaiTroID),
    CONSTRAINT FK_TaiKhoan_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_TaiKhoan_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID)
);
GO

CREATE UNIQUE INDEX UX_TaiKhoan_Admin_Active
ON TaiKhoan(VaiTroID)
WHERE VaiTroID = 1 AND TrangThaiID = 101 AND TrangThaiDuLieuID = 1;
GO

CREATE UNIQUE INDEX UX_TaiKhoan_Manager_Active
ON TaiKhoan(VaiTroID)
WHERE VaiTroID = 2 AND TrangThaiID = 101 AND TrangThaiDuLieuID = 1;
GO

-- 05. TokenDatLaiMatKhau
CREATE TABLE TokenDatLaiMatKhau (
    TokenDatLaiMatKhauID BIGINT IDENTITY(1,1) NOT NULL,
    TaiKhoanID INT NOT NULL,
    TokenHash VARCHAR(255) NOT NULL,
    HetHanLuc DATETIME2(0) NOT NULL,
    DaDungLuc DATETIME2(0) NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_TokenDatLaiMatKhau_NgayTao DEFAULT SYSDATETIME(),
    CONSTRAINT PK_TokenDatLaiMatKhau PRIMARY KEY (TokenDatLaiMatKhauID),
    CONSTRAINT UQ_TokenDatLaiMatKhau_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_TokenDatLaiMatKhau_TaiKhoan FOREIGN KEY (TaiKhoanID) REFERENCES TaiKhoan(TaiKhoanID),
    CONSTRAINT CK_TokenDatLaiMatKhau_HetHan CHECK (HetHanLuc > NgayTao)
);
GO

-- 06. KhachHang
CREATE TABLE KhachHang (
    KhachHangID INT IDENTITY(1,1) NOT NULL,
    MaKhachHang NVARCHAR(20) NOT NULL,
    TaiKhoanID INT NULL,
    HoTen NVARCHAR(150) NOT NULL,
    SoDienThoai VARCHAR(20) NOT NULL,
    Email NVARCHAR(255) NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_KhachHang_NgayTao DEFAULT SYSDATETIME(),
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_KhachHang_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_KhachHang PRIMARY KEY (KhachHangID),
    CONSTRAINT UQ_KhachHang_MaKhachHang UNIQUE (MaKhachHang),
    CONSTRAINT FK_KhachHang_TaiKhoan FOREIGN KEY (TaiKhoanID) REFERENCES TaiKhoan(TaiKhoanID),
    CONSTRAINT FK_KhachHang_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID)
);
GO

CREATE UNIQUE INDEX UX_KhachHang_TaiKhoan
ON KhachHang(TaiKhoanID)
WHERE TaiKhoanID IS NOT NULL;
GO

-- 07. NhanVien
CREATE TABLE NhanVien (
    NhanVienID INT IDENTITY(1,1) NOT NULL,
    MaNhanVien NVARCHAR(20) NOT NULL,
    TaiKhoanID INT NOT NULL,
    HoTen NVARCHAR(150) NOT NULL,
    SoDienThoai VARCHAR(20) NOT NULL,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_NhanVien_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_NhanVien PRIMARY KEY (NhanVienID),
    CONSTRAINT UQ_NhanVien_MaNhanVien UNIQUE (MaNhanVien),
    CONSTRAINT UQ_NhanVien_TaiKhoan UNIQUE (TaiKhoanID),
    CONSTRAINT FK_NhanVien_TaiKhoan FOREIGN KEY (TaiKhoanID) REFERENCES TaiKhoan(TaiKhoanID),
    CONSTRAINT FK_NhanVien_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID)
);
GO

-- 07A. YeuCauThayDoiVaiTro
CREATE TABLE YeuCauThayDoiVaiTro (
    YeuCauThayDoiVaiTroID INT IDENTITY(1,1) NOT NULL,
    NhanVienID INT NOT NULL,
    VaiTroHienTaiID TINYINT NOT NULL,
    VaiTroDeXuatID TINYINT NOT NULL,
    NguoiYeuCauTaiKhoanID INT NOT NULL,
    LyDo NVARCHAR(500) NULL,
    MaNhanVienTruoc NVARCHAR(20) NULL,
    MaNhanVienSau NVARCHAR(20) NULL,
    TrangThai VARCHAR(20) NOT NULL CONSTRAINT DF_YeuCauThayDoiVaiTro_TrangThai DEFAULT 'PENDING',
    NgayYeuCau DATETIME2(0) NOT NULL CONSTRAINT DF_YeuCauThayDoiVaiTro_NgayYeuCau DEFAULT SYSDATETIME(),
    NguoiXuLyTaiKhoanID INT NULL,
    NgayXuLy DATETIME2(0) NULL,
    LyDoTuChoi NVARCHAR(500) NULL,
    CONSTRAINT PK_YeuCauThayDoiVaiTro PRIMARY KEY (YeuCauThayDoiVaiTroID),
    CONSTRAINT FK_YeuCauThayDoiVaiTro_NhanVien FOREIGN KEY (NhanVienID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT FK_YeuCauThayDoiVaiTro_VaiTroHienTai FOREIGN KEY (VaiTroHienTaiID) REFERENCES VaiTro(VaiTroID),
    CONSTRAINT FK_YeuCauThayDoiVaiTro_VaiTroDeXuat FOREIGN KEY (VaiTroDeXuatID) REFERENCES VaiTro(VaiTroID),
    CONSTRAINT FK_YeuCauThayDoiVaiTro_NguoiYeuCau FOREIGN KEY (NguoiYeuCauTaiKhoanID) REFERENCES TaiKhoan(TaiKhoanID),
    CONSTRAINT FK_YeuCauThayDoiVaiTro_NguoiXuLy FOREIGN KEY (NguoiXuLyTaiKhoanID) REFERENCES TaiKhoan(TaiKhoanID),
    CONSTRAINT CK_YeuCauThayDoiVaiTro_TrangThai CHECK (TrangThai IN ('PENDING', 'APPROVED', 'REJECTED')),
    CONSTRAINT CK_YeuCauThayDoiVaiTro_VaiTroKhacNhau CHECK (VaiTroHienTaiID <> VaiTroDeXuatID)
);
GO

CREATE UNIQUE INDEX UX_YeuCauThayDoiVaiTro_NhanVien_PENDING
ON YeuCauThayDoiVaiTro(NhanVienID)
WHERE TrangThai = 'PENDING';
GO

-- 08. SanhTiec
CREATE TABLE SanhTiec (
    SanhTiecID INT IDENTITY(1,1) NOT NULL,
    MaSanh NVARCHAR(20) NOT NULL,
    TenSanh NVARCHAR(150) NOT NULL,
    SucChuaToiThieu INT NULL,
    SucChuaToiDa INT NOT NULL,
    GiaThue DECIMAL(18,2) NOT NULL,
    MoTa NVARCHAR(1000) NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_SanhTiec_TrangThai DEFAULT 201,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_SanhTiec_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_SanhTiec PRIMARY KEY (SanhTiecID),
    CONSTRAINT UQ_SanhTiec_MaSanh UNIQUE (MaSanh),
    CONSTRAINT FK_SanhTiec_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_SanhTiec_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_SanhTiec_SucChuaToiThieu CHECK (SucChuaToiThieu IS NULL OR SucChuaToiThieu > 0),
    CONSTRAINT CK_SanhTiec_SucChuaToiDa CHECK (SucChuaToiDa > 0),
    CONSTRAINT CK_SanhTiec_KhoangSucChua CHECK (SucChuaToiThieu IS NULL OR SucChuaToiThieu <= SucChuaToiDa),
    CONSTRAINT CK_SanhTiec_GiaThue CHECK (GiaThue >= 0)
);
GO

-- 09. LichSanh
CREATE TABLE LichSanh (
    LichSanhID INT IDENTITY(1,1) NOT NULL,
    SanhTiecID INT NOT NULL,
    Ngay DATE NOT NULL,
    CaToChuc NVARCHAR(20) NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_LichSanh_TrangThai DEFAULT 301,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_LichSanh_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_LichSanh PRIMARY KEY (LichSanhID),
    CONSTRAINT FK_LichSanh_SanhTiec FOREIGN KEY (SanhTiecID) REFERENCES SanhTiec(SanhTiecID),
    CONSTRAINT FK_LichSanh_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_LichSanh_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_LichSanh_CaToChuc CHECK (CaToChuc IN (N'Ca trưa', N'Ca tối'))
);
GO

CREATE UNIQUE INDEX UX_LichSanh_Sanh_Ngay_Ca_EXISTING
ON LichSanh(SanhTiecID, Ngay, CaToChuc)
WHERE TrangThaiDuLieuID = 1;
GO

-- 10. ThucDon
CREATE TABLE ThucDon (
    ThucDonID INT IDENTITY(1,1) NOT NULL,
    MaThucDon NVARCHAR(20) NOT NULL,
    TenThucDon NVARCHAR(200) NOT NULL,
    LoaiThucDon VARCHAR(20) NOT NULL,
    KhachHangID INT NULL,
    MoTa NVARCHAR(1000) NULL,
    GiaMoiBan DECIMAL(18,2) NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_ThucDon_TrangThai DEFAULT 401,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_ThucDon_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_ThucDon PRIMARY KEY (ThucDonID),
    CONSTRAINT UQ_ThucDon_MaThucDon UNIQUE (MaThucDon),
    CONSTRAINT FK_ThucDon_KhachHang FOREIGN KEY (KhachHangID) REFERENCES KhachHang(KhachHangID),
    CONSTRAINT FK_ThucDon_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_ThucDon_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_ThucDon_LoaiThucDon CHECK (LoaiThucDon IN ('STANDARD', 'CUSTOM')),
    CONSTRAINT CK_ThucDon_KhachHang CHECK (
        (LoaiThucDon = 'STANDARD' AND KhachHangID IS NULL)
        OR (LoaiThucDon = 'CUSTOM' AND KhachHangID IS NOT NULL)
    ),
    CONSTRAINT CK_ThucDon_GiaMoiBan CHECK (GiaMoiBan >= 0)
);
GO

-- 11. MonAn
CREATE TABLE MonAn (
    MonAnID INT IDENTITY(1,1) NOT NULL,
    MaMon NVARCHAR(20) NOT NULL,
    TenMon NVARCHAR(200) NOT NULL,
    NhomMon NVARCHAR(100) NOT NULL,
    GiaMon DECIMAL(18,2) NOT NULL,
    HinhAnh NVARCHAR(500) NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_MonAn_TrangThai DEFAULT 501,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_MonAn_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_MonAn PRIMARY KEY (MonAnID),
    CONSTRAINT UQ_MonAn_MaMon UNIQUE (MaMon),
    CONSTRAINT FK_MonAn_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_MonAn_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_MonAn_GiaMon CHECK (GiaMon >= 0)
);
GO

-- 12. ChiTietThucDon
CREATE TABLE ChiTietThucDon (
    ChiTietThucDonID INT IDENTITY(1,1) NOT NULL,
    ThucDonID INT NOT NULL,
    MonAnID INT NOT NULL,
    SoThuTu INT NOT NULL,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_ChiTietThucDon_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_ChiTietThucDon PRIMARY KEY (ChiTietThucDonID),
    CONSTRAINT UQ_ChiTietThucDon_ThucDon_MonAn UNIQUE (ThucDonID, MonAnID),
    CONSTRAINT FK_ChiTietThucDon_ThucDon FOREIGN KEY (ThucDonID) REFERENCES ThucDon(ThucDonID),
    CONSTRAINT FK_ChiTietThucDon_MonAn FOREIGN KEY (MonAnID) REFERENCES MonAn(MonAnID),
    CONSTRAINT FK_ChiTietThucDon_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_ChiTietThucDon_SoThuTu CHECK (SoThuTu > 0)
);
GO

CREATE UNIQUE INDEX UX_ChiTietThucDon_ThucDon_SoThuTu_EXISTING
ON ChiTietThucDon(ThucDonID, SoThuTu)
WHERE TrangThaiDuLieuID = 1;
GO

-- 13. GoiTrangTri
CREATE TABLE GoiTrangTri (
    GoiTrangTriID INT IDENTITY(1,1) NOT NULL,
    MaGoi NVARCHAR(20) NOT NULL,
    TenGoi NVARCHAR(200) NOT NULL,
    PhongCach NVARCHAR(100) NOT NULL,
    MoTa NVARCHAR(1000) NULL,
    Gia DECIMAL(18,2) NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_GoiTrangTri_TrangThai DEFAULT 601,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_GoiTrangTri_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_GoiTrangTri PRIMARY KEY (GoiTrangTriID),
    CONSTRAINT UQ_GoiTrangTri_MaGoi UNIQUE (MaGoi),
    CONSTRAINT FK_GoiTrangTri_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_GoiTrangTri_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_GoiTrangTri_Gia CHECK (Gia >= 0)
);
GO

-- 14. DichVu
CREATE TABLE DichVu (
    DichVuID INT IDENTITY(1,1) NOT NULL,
    MaDichVu NVARCHAR(20) NOT NULL,
    TenDichVu NVARCHAR(200) NOT NULL,
    LoaiDichVu NVARCHAR(100) NULL,
    MoTa NVARCHAR(1000) NULL,
    Gia DECIMAL(18,2) NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_DichVu_TrangThai DEFAULT 701,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_DichVu_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_DichVu PRIMARY KEY (DichVuID),
    CONSTRAINT UQ_DichVu_MaDichVu UNIQUE (MaDichVu),
    CONSTRAINT FK_DichVu_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT FK_DichVu_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_DichVu_Gia CHECK (Gia >= 0)
);
GO

-- 15. DatTiec
CREATE TABLE DatTiec (
    DatTiecID INT IDENTITY(1,1) NOT NULL,
    MaDatTiec NVARCHAR(50) NOT NULL,
    KhachHangID INT NOT NULL,
    LichSanhID INT NOT NULL,
    ThucDonID INT NULL,
    GoiTrangTriID INT NULL,
    SoLuongKhach INT NOT NULL,
    GiaThucDonChot DECIMAL(18,2) NULL,
    GiaTrangTriChot DECIMAL(18,2) NULL,
    GiaSanhChot DECIMAL(18,2) NULL,
    TongTienDuKien DECIMAL(18,2) NULL,
    YeuCauDacBiet NVARCHAR(MAX) NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_DatTiec_TrangThai DEFAULT 801,
    LyDoHuy NVARCHAR(500) NULL,
    NgayDat DATETIME2(0) NOT NULL CONSTRAINT DF_DatTiec_NgayDat DEFAULT SYSDATETIME(),
    CONSTRAINT PK_DatTiec PRIMARY KEY (DatTiecID),
    CONSTRAINT UQ_DatTiec_MaDatTiec UNIQUE (MaDatTiec),
    CONSTRAINT FK_DatTiec_KhachHang FOREIGN KEY (KhachHangID) REFERENCES KhachHang(KhachHangID),
    CONSTRAINT FK_DatTiec_LichSanh FOREIGN KEY (LichSanhID) REFERENCES LichSanh(LichSanhID),
    CONSTRAINT FK_DatTiec_ThucDon FOREIGN KEY (ThucDonID) REFERENCES ThucDon(ThucDonID),
    CONSTRAINT FK_DatTiec_GoiTrangTri FOREIGN KEY (GoiTrangTriID) REFERENCES GoiTrangTri(GoiTrangTriID),
    CONSTRAINT FK_DatTiec_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_DatTiec_SoLuongKhach CHECK (SoLuongKhach > 0),
    CONSTRAINT CK_DatTiec_GiaThucDonChot CHECK (GiaThucDonChot IS NULL OR GiaThucDonChot >= 0),
    CONSTRAINT CK_DatTiec_GiaTrangTriChot CHECK (GiaTrangTriChot IS NULL OR GiaTrangTriChot >= 0),
    CONSTRAINT CK_DatTiec_GiaSanhChot CHECK (GiaSanhChot IS NULL OR GiaSanhChot >= 0),
    CONSTRAINT CK_DatTiec_TongTienDuKien CHECK (TongTienDuKien IS NULL OR TongTienDuKien >= 0)
);
GO

-- 16. DatTiec_DichVu
CREATE TABLE DatTiec_DichVu (
    DatTiecDichVuID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    DichVuID INT NOT NULL,
    SoLuong INT NOT NULL CONSTRAINT DF_DatTiec_DichVu_SoLuong DEFAULT 1,
    DonGiaChot DECIMAL(18,2) NOT NULL,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_DatTiec_DichVu_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_DatTiec_DichVu PRIMARY KEY (DatTiecDichVuID),
    CONSTRAINT UQ_DatTiec_DichVu UNIQUE (DatTiecID, DichVuID),
    CONSTRAINT FK_DatTiec_DichVu_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_DatTiec_DichVu_DichVu FOREIGN KEY (DichVuID) REFERENCES DichVu(DichVuID),
    CONSTRAINT FK_DatTiec_DichVu_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_DatTiec_DichVu_SoLuong CHECK (SoLuong > 0),
    CONSTRAINT CK_DatTiec_DichVu_DonGiaChot CHECK (DonGiaChot >= 0)
);
GO

-- 17. HopDong
CREATE TABLE HopDong (
    HopDongID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    MaHopDong NVARCHAR(50) NOT NULL,
    NgayLap DATE NOT NULL CONSTRAINT DF_HopDong_NgayLap DEFAULT (CONVERT(DATE, SYSDATETIME())),
    TongGiaTri DECIMAL(18,2) NOT NULL,
    NoiDungHopDong NVARCHAR(MAX) NULL,
    DieuKhoanThanhToan NVARCHAR(MAX) NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_HopDong_TrangThai DEFAULT 901,
    CONSTRAINT PK_HopDong PRIMARY KEY (HopDongID),
    CONSTRAINT UQ_HopDong_DatTiec UNIQUE (DatTiecID),
    CONSTRAINT UQ_HopDong_MaHopDong UNIQUE (MaHopDong),
    CONSTRAINT FK_HopDong_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_HopDong_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_HopDong_TongGiaTri CHECK (TongGiaTri >= 0)
);
GO

-- 18. ThanhToan
CREATE TABLE ThanhToan (
    ThanhToanID INT IDENTITY(1,1) NOT NULL,
    HopDongID INT NOT NULL,
    LoaiThanhToan NVARCHAR(50) NOT NULL,
    SoTien DECIMAL(18,2) NOT NULL,
    NgayThanhToan DATETIME2(0) NOT NULL CONSTRAINT DF_ThanhToan_NgayThanhToan DEFAULT SYSDATETIME(),
    PhuongThuc NVARCHAR(50) NULL,
    MaGiaoDich NVARCHAR(100) NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_ThanhToan_TrangThai DEFAULT 1001,
    CONSTRAINT PK_ThanhToan PRIMARY KEY (ThanhToanID),
    CONSTRAINT FK_ThanhToan_HopDong FOREIGN KEY (HopDongID) REFERENCES HopDong(HopDongID),
    CONSTRAINT FK_ThanhToan_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_ThanhToan_Loai CHECK (LoaiThanhToan IN (N'Đặt cọc', N'Thanh toán đợt', N'Quyết toán')),
    CONSTRAINT CK_ThanhToan_SoTien CHECK (SoTien > 0)
);
GO

-- 19. PhanCongDieuPhoi
CREATE TABLE PhanCongDieuPhoi (
    PhanCongID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    NhanVienDieuPhoiID INT NOT NULL,
    QuanLySanhPhanCongID INT NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_PhanCongDieuPhoi_TrangThai DEFAULT 1101,
    NgayPhanCong DATETIME2(0) NOT NULL CONSTRAINT DF_PhanCongDieuPhoi_NgayPhanCong DEFAULT SYSDATETIME(),
    CONSTRAINT PK_PhanCongDieuPhoi PRIMARY KEY (PhanCongID),
    CONSTRAINT FK_PhanCongDieuPhoi_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_PhanCongDieuPhoi_NhanVienDieuPhoi FOREIGN KEY (NhanVienDieuPhoiID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT FK_PhanCongDieuPhoi_QuanLySanh FOREIGN KEY (QuanLySanhPhanCongID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT FK_PhanCongDieuPhoi_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID)
);
GO

CREATE UNIQUE INDEX UX_PhanCongDieuPhoi_DatTiec_ASSIGNED
ON PhanCongDieuPhoi(DatTiecID)
WHERE TrangThaiID = 1101;
GO

-- 20. SuCoTiec
CREATE TABLE SuCoTiec (
    SuCoID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    NhanVienBaoCaoID INT NULL,
    LoaiSuCo NVARCHAR(100) NOT NULL,
    MoTa NVARCHAR(MAX) NOT NULL,
    MucDo NVARCHAR(20) NOT NULL CONSTRAINT DF_SuCoTiec_MucDo DEFAULT N'Bình thường',
    TrangThaiID INT NOT NULL CONSTRAINT DF_SuCoTiec_TrangThai DEFAULT 1201,
    HuongXuLy NVARCHAR(MAX) NULL,
    ThoiGianPhatSinh DATETIME2(0) NOT NULL CONSTRAINT DF_SuCoTiec_ThoiGianPhatSinh DEFAULT SYSDATETIME(),
    ThoiGianXuLy DATETIME2(0) NULL,
    CONSTRAINT PK_SuCoTiec PRIMARY KEY (SuCoID),
    CONSTRAINT FK_SuCoTiec_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_SuCoTiec_NhanVienBaoCao FOREIGN KEY (NhanVienBaoCaoID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT FK_SuCoTiec_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_SuCoTiec_MucDo CHECK (MucDo IN (N'Thấp', N'Bình thường', N'Nghiêm trọng')),
    CONSTRAINT CK_SuCoTiec_ThoiGianXuLy CHECK (ThoiGianXuLy IS NULL OR ThoiGianXuLy >= ThoiGianPhatSinh)
);
GO

-- 21. ThayDoiLichTiec
CREATE TABLE ThayDoiLichTiec (
    ThayDoiLichID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    LichSanhCuID INT NOT NULL,
    LichSanhMoiID INT NOT NULL,
    LyDo NVARCHAR(500) NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_ThayDoiLichTiec_TrangThai DEFAULT 1301,
    NgayYeuCau DATETIME2(0) NOT NULL CONSTRAINT DF_ThayDoiLichTiec_NgayYeuCau DEFAULT SYSDATETIME(),
    NgayXuLy DATETIME2(0) NULL,
    CONSTRAINT PK_ThayDoiLichTiec PRIMARY KEY (ThayDoiLichID),
    CONSTRAINT FK_ThayDoiLichTiec_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_ThayDoiLichTiec_LichCu FOREIGN KEY (LichSanhCuID) REFERENCES LichSanh(LichSanhID),
    CONSTRAINT FK_ThayDoiLichTiec_LichMoi FOREIGN KEY (LichSanhMoiID) REFERENCES LichSanh(LichSanhID),
    CONSTRAINT FK_ThayDoiLichTiec_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_ThayDoiLichTiec_KhacLich CHECK (LichSanhCuID <> LichSanhMoiID),
    CONSTRAINT CK_ThayDoiLichTiec_NgayXuLy CHECK (NgayXuLy IS NULL OR NgayXuLy >= NgayYeuCau)
);
GO

-- 22. MaQRDanhGia
CREATE TABLE MaQRDanhGia (
    MaQRDanhGiaID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    MaQR NVARCHAR(150) NOT NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_MaQRDanhGia_NgayTao DEFAULT SYSDATETIME(),
    NgayHetHan DATETIME2(0) NOT NULL,
    TrangThaiID INT NOT NULL CONSTRAINT DF_MaQRDanhGia_TrangThai DEFAULT 1401,
    CONSTRAINT PK_MaQRDanhGia PRIMARY KEY (MaQRDanhGiaID),
    CONSTRAINT UQ_MaQRDanhGia_DatTiec UNIQUE (DatTiecID),
    CONSTRAINT UQ_MaQRDanhGia_MaQR UNIQUE (MaQR),
    CONSTRAINT FK_MaQRDanhGia_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_MaQRDanhGia_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_MaQRDanhGia_NgayHetHan CHECK (NgayHetHan > NgayTao)
);
GO

-- 23. DanhGia
CREATE TABLE DanhGia (
    DanhGiaID INT IDENTITY(1,1) NOT NULL,
    MaQRDanhGiaID INT NOT NULL,
    LoaiNguoiDanhGia NVARCHAR(50) NOT NULL,
    DiemSanh INT NULL,
    DiemMonAn INT NULL,
    DiemPhucVu INT NULL,
    DiemAmThanh INT NULL,
    DiemAnhSang INT NULL,
    DiemTongThe INT NOT NULL,
    BinhLuan NVARCHAR(MAX) NULL,
    NgayDanhGia DATETIME2(0) NOT NULL CONSTRAINT DF_DanhGia_NgayDanhGia DEFAULT SYSDATETIME(),
    CONSTRAINT PK_DanhGia PRIMARY KEY (DanhGiaID),
    CONSTRAINT FK_DanhGia_MaQRDanhGia FOREIGN KEY (MaQRDanhGiaID) REFERENCES MaQRDanhGia(MaQRDanhGiaID),
    CONSTRAINT CK_DanhGia_LoaiNguoiDanhGia CHECK (LoaiNguoiDanhGia IN (N'Khách mời', N'Chủ tiệc')),
    CONSTRAINT CK_DanhGia_DiemSanh CHECK (DiemSanh IS NULL OR DiemSanh BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemMonAn CHECK (DiemMonAn IS NULL OR DiemMonAn BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemPhucVu CHECK (DiemPhucVu IS NULL OR DiemPhucVu BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemAmThanh CHECK (DiemAmThanh IS NULL OR DiemAmThanh BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemAnhSang CHECK (DiemAnhSang IS NULL OR DiemAnhSang BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemTongThe CHECK (DiemTongThe BETWEEN 1 AND 5)
);
GO

-- 24. HinhAnh
CREATE TABLE HinhAnh (
    HinhAnhID INT IDENTITY(1,1) NOT NULL,
    SanhTiecID INT NULL,
    GoiTrangTriID INT NULL,
    DuongDanAnh NVARCHAR(500) NOT NULL,
    LaAnhDaiDien BIT NOT NULL CONSTRAINT DF_HinhAnh_LaAnhDaiDien DEFAULT 0,
    SoThuTu INT NOT NULL CONSTRAINT DF_HinhAnh_SoThuTu DEFAULT 1,
    TrangThaiDuLieuID TINYINT NOT NULL CONSTRAINT DF_HinhAnh_TrangThaiDuLieu DEFAULT 1,
    CONSTRAINT PK_HinhAnh PRIMARY KEY (HinhAnhID),
    CONSTRAINT FK_HinhAnh_SanhTiec FOREIGN KEY (SanhTiecID) REFERENCES SanhTiec(SanhTiecID),
    CONSTRAINT FK_HinhAnh_GoiTrangTri FOREIGN KEY (GoiTrangTriID) REFERENCES GoiTrangTri(GoiTrangTriID),
    CONSTRAINT FK_HinhAnh_TrangThaiDuLieu FOREIGN KEY (TrangThaiDuLieuID) REFERENCES TrangThaiDuLieu(TrangThaiDuLieuID),
    CONSTRAINT CK_HinhAnh_MotDoiTuong CHECK (
        (SanhTiecID IS NOT NULL AND GoiTrangTriID IS NULL)
        OR (SanhTiecID IS NULL AND GoiTrangTriID IS NOT NULL)
    ),
    CONSTRAINT CK_HinhAnh_SoThuTu CHECK (SoThuTu > 0)
);
GO

CREATE UNIQUE INDEX UX_HinhAnh_Sanh_AnhDaiDien
ON HinhAnh(SanhTiecID)
WHERE SanhTiecID IS NOT NULL AND LaAnhDaiDien = 1 AND TrangThaiDuLieuID = 1;
GO

CREATE UNIQUE INDEX UX_HinhAnh_GoiTrangTri_AnhDaiDien
ON HinhAnh(GoiTrangTriID)
WHERE GoiTrangTriID IS NOT NULL AND LaAnhDaiDien = 1 AND TrangThaiDuLieuID = 1;
GO

-- 25. PhanCongQuanLySanh
CREATE TABLE PhanCongQuanLySanh (
    PhanCongQuanLySanhID INT IDENTITY(1,1) NOT NULL,
    SanhTiecID INT NOT NULL,
    NhanVienQuanLySanhID INT NOT NULL,
    TuNgay DATE NOT NULL CONSTRAINT DF_PhanCongQuanLySanh_TuNgay DEFAULT (CONVERT(DATE, SYSDATETIME())),
    DenNgay DATE NULL,
    CONSTRAINT PK_PhanCongQuanLySanh PRIMARY KEY (PhanCongQuanLySanhID),
    CONSTRAINT FK_PhanCongQuanLySanh_SanhTiec FOREIGN KEY (SanhTiecID) REFERENCES SanhTiec(SanhTiecID),
    CONSTRAINT FK_PhanCongQuanLySanh_NhanVien FOREIGN KEY (NhanVienQuanLySanhID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT CK_PhanCongQuanLySanh_ThoiGian CHECK (DenNgay IS NULL OR DenNgay >= TuNgay)
);
GO

CREATE UNIQUE INDEX UX_PhanCongQuanLySanh_HienTai
ON PhanCongQuanLySanh(SanhTiecID)
WHERE DenNgay IS NULL;
GO

-- 26. YeuCauKhuyenNghi
CREATE TABLE YeuCauKhuyenNghi (
    YeuCauKhuyenNghiID BIGINT IDENTITY(1,1) NOT NULL,
    KhachHangID INT NULL,
    NganSachDuKien DECIMAL(18,2) NOT NULL,
    SoLuongKhach INT NOT NULL,
    NgayToChucMongMuon DATE NOT NULL,
    CaToChucMongMuon NVARCHAR(20) NOT NULL,
    PhongCachMongMuon NVARCHAR(100) NOT NULL,
    NhuCauDichVu NVARCHAR(500) NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_YeuCauKhuyenNghi_NgayTao DEFAULT SYSDATETIME(),
    CONSTRAINT PK_YeuCauKhuyenNghi PRIMARY KEY (YeuCauKhuyenNghiID),
    CONSTRAINT FK_YeuCauKhuyenNghi_KhachHang FOREIGN KEY (KhachHangID) REFERENCES KhachHang(KhachHangID),
    CONSTRAINT CK_YeuCauKhuyenNghi_NganSach CHECK (NganSachDuKien >= 0),
    CONSTRAINT CK_YeuCauKhuyenNghi_SoLuongKhach CHECK (SoLuongKhach > 0),
    CONSTRAINT CK_YeuCauKhuyenNghi_Ca CHECK (CaToChucMongMuon IN (N'Ca trưa', N'Ca tối'))
);
GO

-- 27. NhatKyThaoTac
CREATE TABLE NhatKyThaoTac (
    NhatKyThaoTacID BIGINT IDENTITY(1,1) NOT NULL,
    TaiKhoanID INT NULL,
    HanhDong VARCHAR(30) NOT NULL,
    DoiTuong NVARCHAR(100) NOT NULL,
    DoiTuongID BIGINT NOT NULL,
    DuLieuCu NVARCHAR(MAX) NULL,
    DuLieuMoi NVARCHAR(MAX) NULL,
    ThoiGian DATETIME2(0) NOT NULL CONSTRAINT DF_NhatKyThaoTac_ThoiGian DEFAULT SYSDATETIME(),
    GhiChu NVARCHAR(500) NULL,
    CONSTRAINT PK_NhatKyThaoTac PRIMARY KEY (NhatKyThaoTacID),
    CONSTRAINT FK_NhatKyThaoTac_TaiKhoan FOREIGN KEY (TaiKhoanID) REFERENCES TaiKhoan(TaiKhoanID)
);
GO

CREATE INDEX IX_NhatKyThaoTac_ThoiGian_ID ON NhatKyThaoTac(ThoiGian DESC, NhatKyThaoTacID DESC);
GO
CREATE INDEX IX_NhatKyThaoTac_HanhDong_ThoiGian ON NhatKyThaoTac(HanhDong, ThoiGian DESC);
GO

-- 28. ThongBao
CREATE TABLE ThongBao (
    ThongBaoID INT IDENTITY(1,1) NOT NULL,
    TaiKhoanNhanID INT NOT NULL,
    Loai VARCHAR(50) NOT NULL,
    TieuDe NVARCHAR(200) NOT NULL,
    NoiDung NVARCHAR(1000) NOT NULL,
    LoaiDoiTuong VARCHAR(50) NULL,
    DoiTuongID INT NULL,
    DaDoc BIT NOT NULL CONSTRAINT DF_ThongBao_DaDoc DEFAULT 0,
    NgayDoc DATETIME2(0) NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_ThongBao_NgayTao DEFAULT SYSDATETIME(),
    CONSTRAINT PK_ThongBao PRIMARY KEY (ThongBaoID),
    CONSTRAINT FK_ThongBao_TaiKhoanNhan FOREIGN KEY (TaiKhoanNhanID) REFERENCES TaiKhoan(TaiKhoanID)
);
GO
CREATE INDEX IX_ThongBao_NguoiNhan_DaDoc_NgayTao ON ThongBao(TaiKhoanNhanID, DaDoc, NgayTao DESC);
GO

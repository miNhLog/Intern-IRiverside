SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuCauThayDoiVaiTro (
        YeuCauThayDoiVaiTroID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_YeuCauThayDoiVaiTro PRIMARY KEY,
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
        CONSTRAINT FK_YeuCauThayDoiVaiTro_NhanVien FOREIGN KEY (NhanVienID) REFERENCES dbo.NhanVien(NhanVienID),
        CONSTRAINT FK_YeuCauThayDoiVaiTro_VaiTroHienTai FOREIGN KEY (VaiTroHienTaiID) REFERENCES dbo.VaiTro(VaiTroID),
        CONSTRAINT FK_YeuCauThayDoiVaiTro_VaiTroDeXuat FOREIGN KEY (VaiTroDeXuatID) REFERENCES dbo.VaiTro(VaiTroID),
        CONSTRAINT FK_YeuCauThayDoiVaiTro_NguoiYeuCau FOREIGN KEY (NguoiYeuCauTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID),
        CONSTRAINT FK_YeuCauThayDoiVaiTro_NguoiXuLy FOREIGN KEY (NguoiXuLyTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID),
        CONSTRAINT CK_YeuCauThayDoiVaiTro_TrangThai CHECK (TrangThai IN ('PENDING','APPROVED','REJECTED')),
        CONSTRAINT CK_YeuCauThayDoiVaiTro_VaiTroKhacNhau CHECK (VaiTroHienTaiID <> VaiTroDeXuatID)
    );
END;

IF COL_LENGTH('dbo.YeuCauThayDoiVaiTro', 'MaNhanVienTruoc') IS NULL
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD MaNhanVienTruoc NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.YeuCauThayDoiVaiTro', 'MaNhanVienSau') IS NULL
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD MaNhanVienSau NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'CK_YeuCauThayDoiVaiTro_TrangThai')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT CK_YeuCauThayDoiVaiTro_TrangThai CHECK (TrangThai IN ('PENDING','APPROVED','REJECTED'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'CK_YeuCauThayDoiVaiTro_VaiTroKhacNhau')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT CK_YeuCauThayDoiVaiTro_VaiTroKhacNhau CHECK (VaiTroHienTaiID <> VaiTroDeXuatID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'FK_YeuCauThayDoiVaiTro_NhanVien')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT FK_YeuCauThayDoiVaiTro_NhanVien FOREIGN KEY (NhanVienID) REFERENCES dbo.NhanVien(NhanVienID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'FK_YeuCauThayDoiVaiTro_VaiTroHienTai')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT FK_YeuCauThayDoiVaiTro_VaiTroHienTai FOREIGN KEY (VaiTroHienTaiID) REFERENCES dbo.VaiTro(VaiTroID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'FK_YeuCauThayDoiVaiTro_VaiTroDeXuat')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT FK_YeuCauThayDoiVaiTro_VaiTroDeXuat FOREIGN KEY (VaiTroDeXuatID) REFERENCES dbo.VaiTro(VaiTroID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'FK_YeuCauThayDoiVaiTro_NguoiYeuCau')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT FK_YeuCauThayDoiVaiTro_NguoiYeuCau FOREIGN KEY (NguoiYeuCauTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'FK_YeuCauThayDoiVaiTro_NguoiXuLy')
    ALTER TABLE dbo.YeuCauThayDoiVaiTro ADD CONSTRAINT FK_YeuCauThayDoiVaiTro_NguoiXuLy FOREIGN KEY (NguoiXuLyTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.YeuCauThayDoiVaiTro') AND name = N'UX_YeuCauThayDoiVaiTro_NhanVien_PENDING')
    CREATE UNIQUE INDEX UX_YeuCauThayDoiVaiTro_NhanVien_PENDING ON dbo.YeuCauThayDoiVaiTro(NhanVienID) WHERE TrangThai = 'PENDING';

COMMIT TRANSACTION;

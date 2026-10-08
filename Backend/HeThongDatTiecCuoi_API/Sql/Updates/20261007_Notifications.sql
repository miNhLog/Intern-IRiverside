SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ThongBao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ThongBao (
        ThongBaoID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ThongBao PRIMARY KEY,
        TaiKhoanNhanID INT NOT NULL,
        Loai VARCHAR(50) NOT NULL,
        TieuDe NVARCHAR(200) NOT NULL,
        NoiDung NVARCHAR(1000) NOT NULL,
        LoaiDoiTuong VARCHAR(50) NULL,
        DoiTuongID INT NULL,
        DaDoc BIT NOT NULL CONSTRAINT DF_ThongBao_DaDoc DEFAULT 0,
        NgayDoc DATETIME2(0) NULL,
        NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_ThongBao_NgayTao DEFAULT SYSDATETIME()
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.ThongBao') AND name=N'FK_ThongBao_TaiKhoanNhan')
    ALTER TABLE dbo.ThongBao ADD CONSTRAINT FK_ThongBao_TaiKhoanNhan FOREIGN KEY (TaiKhoanNhanID) REFERENCES dbo.TaiKhoan(TaiKhoanID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThongBao') AND name=N'IX_ThongBao_NguoiNhan_DaDoc_NgayTao')
    CREATE INDEX IX_ThongBao_NguoiNhan_DaDoc_NgayTao ON dbo.ThongBao(TaiKhoanNhanID, DaDoc, NgayTao DESC);

COMMIT TRANSACTION;

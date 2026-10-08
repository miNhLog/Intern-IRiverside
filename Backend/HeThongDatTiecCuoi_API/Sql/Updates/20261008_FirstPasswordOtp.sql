SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.MaOtpDoiMatKhauLanDau', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MaOtpDoiMatKhauLanDau (
        MaOtpID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaOtpDoiMatKhauLanDau PRIMARY KEY,
        TaiKhoanID INT NOT NULL,
        MaHash NVARCHAR(255) NOT NULL,
        SoLanNhapSai INT NOT NULL CONSTRAINT DF_MaOtpDoiMatKhauLanDau_SoLanNhapSai DEFAULT 0,
        HetHanLuc DATETIME2(0) NOT NULL,
        DaDungLuc DATETIME2(0) NULL,
        NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_MaOtpDoiMatKhauLanDau_NgayTao DEFAULT SYSDATETIME(),
        CONSTRAINT FK_MaOtpDoiMatKhauLanDau_TaiKhoan FOREIGN KEY (TaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID),
        CONSTRAINT CK_MaOtpDoiMatKhauLanDau_HetHan CHECK (HetHanLuc > NgayTao),
        CONSTRAINT CK_MaOtpDoiMatKhauLanDau_SoLanNhapSai CHECK (SoLanNhapSai BETWEEN 0 AND 5)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MaOtpDoiMatKhauLanDau') AND name = N'IX_MaOtpDoiMatKhauLanDau_TaiKhoan_NgayTao')
    CREATE INDEX IX_MaOtpDoiMatKhauLanDau_TaiKhoan_NgayTao ON dbo.MaOtpDoiMatKhauLanDau(TaiKhoanID, NgayTao DESC);

COMMIT TRANSACTION;

USE [IRiversidePalaceDB];
GO

-- 1. Thêm cột TienCoc và SoTienDaThanhToan vào bảng DatTiec nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DatTiec') AND name = 'TienCoc')
BEGIN
    ALTER TABLE DatTiec ADD TienCoc DECIMAL(18,2) NULL CONSTRAINT DF_DatTiec_TienCoc DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DatTiec') AND name = 'SoTienDaThanhToan')
BEGIN
    ALTER TABLE DatTiec ADD SoTienDaThanhToan DECIMAL(18,2) NOT NULL CONSTRAINT DF_DatTiec_SoTienDaThanhToan DEFAULT 0;
END
GO

-- 2. Tạo bảng GiaoDichThanhToan
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GiaoDichThanhToan')
BEGIN
    CREATE TABLE GiaoDichThanhToan (
        GiaoDichID BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DatTiecID INT NOT NULL,
        OrderCode BIGINT NOT NULL CONSTRAINT UQ_GiaoDichThanhToan_OrderCode UNIQUE,
        SoTienGiaoDich DECIMAL(18,2) NOT NULL,
        SoTienGhiNhan DECIMAL(18,2) NOT NULL,
        PhuongThuc NVARCHAR(50) NOT NULL CONSTRAINT DF_GiaoDichThanhToan_PhuongThuc DEFAULT N'VIETQR_PAYOS',
        TrangThai NVARCHAR(30) NOT NULL CONSTRAINT DF_GiaoDichThanhToan_TrangThai DEFAULT N'PENDING',
        NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_GiaoDichThanhToan_NgayTao DEFAULT SYSDATETIME(),
        NgayThanhToan DATETIME2(0) NULL,
        PayloadWebhook NVARCHAR(MAX) NULL,
        CONSTRAINT FK_GiaoDichThanhToan_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID)
    );
    CREATE INDEX IX_GiaoDichThanhToan_DatTiecID ON GiaoDichThanhToan(DatTiecID);
    CREATE INDEX IX_GiaoDichThanhToan_TrangThai ON GiaoDichThanhToan(TrangThai);
END
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.GiaoDichThanhToanSuKien', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GiaoDichThanhToanSuKien
    (
        SuKienID BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_GiaoDichThanhToanSuKien PRIMARY KEY,
        GiaoDichID BIGINT NOT NULL,
        LoaiSuKien VARCHAR(50) NOT NULL,
        ThoiDiem DATETIME2(0) NOT NULL
            CONSTRAINT DF_GiaoDichThanhToanSuKien_ThoiDiem DEFAULT SYSDATETIME(),
        ChiTiet NVARCHAR(MAX) NULL,
        CONSTRAINT FK_GiaoDichThanhToanSuKien_GiaoDichThanhToan
            FOREIGN KEY (GiaoDichID) REFERENCES dbo.GiaoDichThanhToan(GiaoDichID),
        CONSTRAINT UX_GiaoDichThanhToanSuKien_GiaoDich_Loai
            UNIQUE (GiaoDichID, LoaiSuKien)
    );
END;

COMMIT TRANSACTION;

-- Danh sách công việc điều phối theo từng tiệc.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DanhSachCongViecTiec', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DanhSachCongViecTiec (
            DatTiecID INT NOT NULL CONSTRAINT PK_DanhSachCongViecTiec PRIMARY KEY,
            TrangThai NVARCHAR(20) NOT NULL CONSTRAINT DF_DanhSachCongViecTiec_TrangThai DEFAULT N'active',
            PhienBan INT NOT NULL CONSTRAINT DF_DanhSachCongViecTiec_PhienBan DEFAULT 1,
            LyDoYeuCauLamLai NVARCHAR(500) NULL,
            CONSTRAINT FK_DanhSachCongViecTiec_DatTiec FOREIGN KEY (DatTiecID) REFERENCES dbo.DatTiec(DatTiecID),
            CONSTRAINT CK_DanhSachCongViecTiec_TrangThai CHECK (TrangThai IN (N'active', N'review', N'approved', N'cancelled'))
        );
    END;

    IF OBJECT_ID(N'dbo.CongViecDieuPhoi', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CongViecDieuPhoi (
            CongViecID INT IDENTITY CONSTRAINT PK_CongViecDieuPhoi PRIMARY KEY,
            DatTiecID INT NOT NULL,
            TenCongViec NVARCHAR(180) NOT NULL,
            GiaiDoan NVARCHAR(30) NOT NULL,
            SoNgaySoVoiNgayTiec INT NOT NULL,
            BatBuoc BIT NOT NULL CONSTRAINT DF_CongViecDieuPhoi_BatBuoc DEFAULT 1,
            TrangThai NVARCHAR(20) NOT NULL CONSTRAINT DF_CongViecDieuPhoi_TrangThai DEFAULT N'todo',
            GhiChu NVARCHAR(500) NULL,
            NguoiCapNhat NVARCHAR(100) NULL,
            NgayCapNhat DATETIME2 NULL,
            CONSTRAINT FK_CongViecDieuPhoi_DanhSach FOREIGN KEY (DatTiecID) REFERENCES dbo.DanhSachCongViecTiec(DatTiecID),
            CONSTRAINT CK_CongViecDieuPhoi_TrangThai CHECK (TrangThai IN (N'todo', N'doing', N'done')),
            CONSTRAINT CK_CongViecDieuPhoi_GiaiDoan CHECK (GiaiDoan IN (N'Trước tiệc', N'Trong tiệc', N'Sau tiệc'))
        );
        CREATE INDEX IX_CongViecDieuPhoi_DatTiecID ON dbo.CongViecDieuPhoi(DatTiecID);
    END;

    IF OBJECT_ID(N'dbo.LichSuCongViecTiec', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LichSuCongViecTiec (
            LichSuID BIGINT IDENTITY CONSTRAINT PK_LichSuCongViecTiec PRIMARY KEY,
            DatTiecID INT NOT NULL,
            TaiKhoanThucHienID INT NULL,
            NguoiThucHien NVARCHAR(100) NOT NULL,
            HanhDong NVARCHAR(40) NOT NULL,
            NoiDung NVARCHAR(2000) NOT NULL,
            ThoiDiem DATETIME2 NOT NULL,
            CONSTRAINT FK_LichSuCongViecTiec_DanhSach FOREIGN KEY (DatTiecID) REFERENCES dbo.DanhSachCongViecTiec(DatTiecID)
        );
        CREATE INDEX IX_LichSuCongViecTiec_DatTiecID_ThoiDiem ON dbo.LichSuCongViecTiec(DatTiecID, ThoiDiem);
    END;

    -- Chỉ khởi tạo cho phân công đang hiệu lực và chưa có danh sách công việc.
    DECLARE @TiecVuaTao TABLE (DatTiecID INT);
    INSERT dbo.DanhSachCongViecTiec (DatTiecID, TrangThai, PhienBan)
    OUTPUT inserted.DatTiecID INTO @TiecVuaTao
    SELECT DISTINCT PhanCong.DatTiecID, N'active', 1
    FROM dbo.PhanCongDieuPhoi PhanCong
    JOIN dbo.TrangThai TrangThaiPhanCong ON TrangThaiPhanCong.TrangThaiID = PhanCong.TrangThaiID
    JOIN dbo.DatTiec Tiec ON Tiec.DatTiecID = PhanCong.DatTiecID
    JOIN dbo.TrangThai TrangThaiTiec ON TrangThaiTiec.TrangThaiID = Tiec.TrangThaiID
    WHERE TrangThaiPhanCong.MaTrangThai = 'ASSIGNED'
      AND TrangThaiTiec.MaTrangThai NOT IN ('CANCELLED', 'COMPLETED')
      AND NOT EXISTS (
          SELECT 1 FROM dbo.DanhSachCongViecTiec DanhSach WITH (UPDLOCK,HOLDLOCK)
          WHERE DanhSach.DatTiecID = PhanCong.DatTiecID
      );

    INSERT dbo.CongViecDieuPhoi (DatTiecID, TenCongViec, GiaiDoan, SoNgaySoVoiNgayTiec, BatBuoc, TrangThai)
    SELECT Tiec.DatTiecID, Mau.TenCongViec, Mau.GiaiDoan, Mau.SoNgaySoVoiNgayTiec, 1, N'todo'
    FROM @TiecVuaTao Tiec CROSS JOIN (VALUES
        (N'Kiểm tra bố trí bàn và sơ đồ sảnh', N'Trước tiệc', -1),
        (N'Xác nhận thực đơn và số bàn với bếp', N'Trước tiệc', -1),
        (N'Kiểm tra trang trí, âm thanh và ánh sáng', N'Trước tiệc', 0),
        (N'Đối chiếu yêu cầu đặc biệt của khách', N'Trước tiệc', 0),
        (N'Theo dõi đón khách và phục vụ món', N'Trong tiệc', 0),
        (N'Theo dõi dịch vụ và các phát sinh trong tiệc', N'Trong tiệc', 0),
        (N'Kiểm tra bàn giao sảnh và thiết bị', N'Sau tiệc', 0),
        (N'Ghi nhận tổng kết ca điều phối', N'Sau tiệc', 1)
    ) Mau(TenCongViec, GiaiDoan, SoNgaySoVoiNgayTiec);

    INSERT dbo.LichSuCongViecTiec (DatTiecID, NguoiThucHien, HanhDong, NoiDung, ThoiDiem)
    SELECT DatTiecID, N'Hệ thống', N'CREATED', N'Khởi tạo checklist tiêu chuẩn cho phân công hiện có.',
        CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time' AS DATETIME2)
    FROM @TiecVuaTao;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;

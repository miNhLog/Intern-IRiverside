SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_ThanhToan_Loai' AND parent_object_id=OBJECT_ID('dbo.ThanhToan'))
 ALTER TABLE dbo.ThanhToan DROP CONSTRAINT CK_ThanhToan_Loai;
ALTER TABLE dbo.ThanhToan WITH CHECK ADD CONSTRAINT CK_ThanhToan_Loai
 CHECK(LoaiThanhToan IN(N'Đặt cọc',N'Thanh toán đợt',N'Quyết toán',N'Điều chỉnh đổi lịch'));

-- Đồng bộ tối thiểu schema PayOS hiện hành cho database cũ chưa chạy migration cọc.
IF COL_LENGTH('dbo.DatTiec','TienCoc') IS NULL
 ALTER TABLE dbo.DatTiec ADD TienCoc DECIMAL(18,2) NULL CONSTRAINT DF_DatTiec_TienCoc DEFAULT 0;
IF COL_LENGTH('dbo.DatTiec','SoTienDaThanhToan') IS NULL
 ALTER TABLE dbo.DatTiec ADD SoTienDaThanhToan DECIMAL(18,2) NOT NULL CONSTRAINT DF_DatTiec_SoTienDaThanhToan DEFAULT 0;

MERGE dbo.TrangThai AS target
USING (VALUES
 (1301, 'PENDING', N'Chờ tiếp nhận', 'SCHEDULE_CHANGE', N'Yêu cầu mới được gửi'),
 (1302, 'APPROVED', N'Đã đổi lịch', 'SCHEDULE_CHANGE', N'Lịch đã được chuyển thành công'),
 (1303, 'REJECTED', N'Từ chối', 'SCHEDULE_CHANGE', N'Yêu cầu bị Manager từ chối'),
 (1304, 'UNDER_REVIEW', N'Đang kiểm tra', 'SCHEDULE_CHANGE', N'Hall Manager đang kiểm tra nghiệp vụ'),
 (1305, 'AWAITING_QUOTE_ACCEPTANCE', N'Chờ khách xác nhận báo giá', 'SCHEDULE_CHANGE', N'Manager đã duyệt có điều kiện'),
 (1306, 'PAYMENT_PENDING', N'Chờ thanh toán', 'SCHEDULE_CHANGE', N'Khách đã xác nhận báo giá và đang giữ lịch'),
 (1307, 'CANCELLED', N'Đã hủy', 'SCHEDULE_CHANGE', N'Yêu cầu được rút hoặc hủy'),
 (1308, 'EXPIRED', N'Hết hạn', 'SCHEDULE_CHANGE', N'Hết thời gian thanh toán hoặc giữ lịch'),
 (1309, 'RECONCILIATION_REQUIRED', N'Cần đối soát', 'SCHEDULE_CHANGE', N'Đã nhận tiền nhưng chưa thể chuyển lịch')
) AS source(TrangThaiID, MaTrangThai, TenTrangThai, NhomTrangThai, MoTa)
ON target.TrangThaiID = source.TrangThaiID
WHEN MATCHED THEN UPDATE SET MaTrangThai=source.MaTrangThai, TenTrangThai=source.TenTrangThai,
    NhomTrangThai=source.NhomTrangThai, MoTa=source.MoTa
WHEN NOT MATCHED THEN INSERT(TrangThaiID, MaTrangThai, TenTrangThai, NhomTrangThai, MoTa)
    VALUES(source.TrangThaiID, source.MaTrangThai, source.TenTrangThai, source.NhomTrangThai, source.MoTa);

IF OBJECT_ID('dbo.ChinhSachDoiLich', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChinhSachDoiLich (
        ChinhSachID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChinhSachDoiLich PRIMARY KEY,
        MaChinhSach VARCHAR(30) NOT NULL,
        PhienBan INT NOT NULL,
        SoNgayToiThieuOnline INT NOT NULL,
        SoNgayToiThieuNgayMoi INT NOT NULL,
        SoPhutThanhToan INT NOT NULL,
        SoPhutGiuLich INT NOT NULL,
        TrangThai VARCHAR(20) NOT NULL,
        HieuLucTu DATETIME2(0) NOT NULL,
        HieuLucDen DATETIME2(0) NULL,
        NguoiTaoTaiKhoanID INT NULL,
        NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_ChinhSachDoiLich_NgayTao DEFAULT SYSDATETIME(),
        GhiChuThayDoi NVARCHAR(1000) NULL,
        CONSTRAINT UX_ChinhSachDoiLich_Ma_PhienBan UNIQUE(MaChinhSach, PhienBan),
        CONSTRAINT CK_ChinhSachDoiLich_Moc CHECK(SoNgayToiThieuOnline > 0 AND SoNgayToiThieuNgayMoi > 0 AND SoPhutThanhToan > 0 AND SoPhutGiuLich >= SoPhutThanhToan),
        CONSTRAINT CK_ChinhSachDoiLich_TrangThai CHECK(TrangThai IN ('DRAFT','ACTIVE','RETIRED')),
        CONSTRAINT CK_ChinhSachDoiLich_HieuLuc CHECK(HieuLucDen IS NULL OR HieuLucDen > HieuLucTu),
        CONSTRAINT FK_ChinhSachDoiLich_NguoiTao FOREIGN KEY(NguoiTaoTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID)
    );
END;

IF OBJECT_ID('dbo.MucPhiDoiLich', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MucPhiDoiLich (
        MucPhiID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MucPhiDoiLich PRIMARY KEY,
        ChinhSachID INT NOT NULL,
        TuSoNgay INT NOT NULL,
        DenSoNgay INT NULL,
        MucPhi DECIMAL(18,2) NOT NULL,
        ChoPhepKhachOnline BIT NOT NULL,
        CONSTRAINT UX_MucPhiDoiLich UNIQUE(ChinhSachID, TuSoNgay, DenSoNgay),
        CONSTRAINT CK_MucPhiDoiLich CHECK(TuSoNgay >= 0 AND (DenSoNgay IS NULL OR DenSoNgay >= TuSoNgay) AND MucPhi >= 0),
        CONSTRAINT FK_MucPhiDoiLich_ChinhSach FOREIGN KEY(ChinhSachID) REFERENCES dbo.ChinhSachDoiLich(ChinhSachID)
    );
END;

IF NOT EXISTS(SELECT 1 FROM dbo.ChinhSachDoiLich WHERE MaChinhSach='POL-CHG-05')
BEGIN
    INSERT dbo.ChinhSachDoiLich(MaChinhSach, PhienBan, SoNgayToiThieuOnline, SoNgayToiThieuNgayMoi,
        SoPhutThanhToan, SoPhutGiuLich, TrangThai, HieuLucTu, GhiChuThayDoi)
    VALUES('POL-CHG-05', 1, 7, 14, 15, 20, 'ACTIVE', '2026-01-01', N'Phiên bản chuẩn giai đoạn 1');
    DECLARE @PolicyId INT = SCOPE_IDENTITY();
    INSERT dbo.MucPhiDoiLich(ChinhSachID, TuSoNgay, DenSoNgay, MucPhi, ChoPhepKhachOnline) VALUES
      (@PolicyId, 0, 6, 5000000, 0),
      (@PolicyId, 7, 29, 5000000, 1),
      (@PolicyId, 30, 44, 2000000, 1),
      (@PolicyId, 45, NULL, 0, 1);
END;

IF COL_LENGTH('dbo.ThayDoiLichTiec','NguonYeuCau') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NguonYeuCau VARCHAR(20) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','NguoiTaoTaiKhoanID') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NguoiTaoTaiKhoanID INT NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','NguoiKiemTraTaiKhoanID') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NguoiKiemTraTaiKhoanID INT NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','NguoiPheDuyetTaiKhoanID') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NguoiPheDuyetTaiKhoanID INT NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','LyDoNgoaiLe') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD LyDoNgoaiLe NVARCHAR(1000) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','BangChungDongYKhach') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD BangChungDongYKhach NVARCHAR(2000) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','KetQuaKiemTra') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD KetQuaKiemTra NVARCHAR(2000) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','LyDoXuLy') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD LyDoXuLy NVARCHAR(1000) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','NgayKiemTra') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NgayKiemTra DATETIME2(0) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','NgayKhachXacNhan') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NgayKhachXacNhan DATETIME2(0) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','NgayThanhToan') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD NgayThanhToan DATETIME2(0) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','ChinhSachID') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD ChinhSachID INT NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','PhienBanChinhSach') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD PhienBanChinhSach INT NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','SoNgayBaoTruoc') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD SoNgayBaoTruoc INT NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','PhiApDung') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD PhiApDung DECIMAL(18,2) NULL;
IF COL_LENGTH('dbo.ThayDoiLichTiec','RowVersion') IS NULL ALTER TABLE dbo.ThayDoiLichTiec ADD RowVersion ROWVERSION;

DECLARE @SeedPolicyId INT = (SELECT TOP(1) ChinhSachID FROM dbo.ChinhSachDoiLich WHERE MaChinhSach='POL-CHG-05' ORDER BY PhienBan DESC);
DECLARE @SeedPolicyVersion INT = (SELECT PhienBan FROM dbo.ChinhSachDoiLich WHERE ChinhSachID=@SeedPolicyId);
EXEC sys.sp_executesql N'UPDATE dbo.ThayDoiLichTiec SET NguonYeuCau=COALESCE(NguonYeuCau,''HALL_MANAGER''),
 ChinhSachID=COALESCE(ChinhSachID,@PolicyId), PhienBanChinhSach=COALESCE(PhienBanChinhSach,@PolicyVersion),
 SoNgayBaoTruoc=COALESCE(SoNgayBaoTruoc,0), PhiApDung=COALESCE(PhiApDung,0);',
 N'@PolicyId INT, @PolicyVersion INT', @SeedPolicyId, @SeedPolicyVersion;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ThayDoiLichTiec') AND name='NguonYeuCau' AND is_nullable=1)
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ALTER COLUMN NguonYeuCau VARCHAR(20) NOT NULL;');
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ThayDoiLichTiec') AND name='ChinhSachID' AND is_nullable=1)
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ALTER COLUMN ChinhSachID INT NOT NULL;');
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ThayDoiLichTiec') AND name='PhienBanChinhSach' AND is_nullable=1)
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ALTER COLUMN PhienBanChinhSach INT NOT NULL;');
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ThayDoiLichTiec') AND name='SoNgayBaoTruoc' AND is_nullable=1)
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ALTER COLUMN SoNgayBaoTruoc INT NOT NULL;');
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ThayDoiLichTiec') AND name='PhiApDung' AND is_nullable=1)
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ALTER COLUMN PhiApDung DECIMAL(18,2) NOT NULL;');

IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ThayDoiLichTiec_ChinhSach')
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ADD CONSTRAINT FK_ThayDoiLichTiec_ChinhSach FOREIGN KEY(ChinhSachID) REFERENCES dbo.ChinhSachDoiLich(ChinhSachID);');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ThayDoiLichTiec_NguoiTao')
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ADD CONSTRAINT FK_ThayDoiLichTiec_NguoiTao FOREIGN KEY(NguoiTaoTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID);');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ThayDoiLichTiec_NguoiKiemTra')
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ADD CONSTRAINT FK_ThayDoiLichTiec_NguoiKiemTra FOREIGN KEY(NguoiKiemTraTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID);');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ThayDoiLichTiec_NguoiPheDuyet')
 EXEC(N'ALTER TABLE dbo.ThayDoiLichTiec ADD CONSTRAINT FK_ThayDoiLichTiec_NguoiPheDuyet FOREIGN KEY(NguoiPheDuyetTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID);');

IF OBJECT_ID('dbo.BaoGiaDoiLich','U') IS NULL
CREATE TABLE dbo.BaoGiaDoiLich(
 BaoGiaID INT IDENTITY PRIMARY KEY, ThayDoiLichID INT NOT NULL, PhienBan INT NOT NULL,
 GiaSanhCu DECIMAL(18,2) NOT NULL, GiaSanhMoi DECIMAL(18,2) NOT NULL, ChenhLechGiaSanh DECIMAL(18,2) NOT NULL,
 PhiDoiLich DECIMAL(18,2) NOT NULL, TongDieuChinh DECIMAL(18,2) NOT NULL,
 GiaTriHopDongTruoc DECIMAL(18,2) NOT NULL, GiaTriHopDongSau DECIMAL(18,2) NOT NULL,
 DaThanhToanSnapshot DECIMAL(18,2) NOT NULL, SoTienCanThu DECIMAL(18,2) NOT NULL,
 SoTienKhauTru DECIMAL(18,2) NOT NULL, SoTienCanHoan DECIMAL(18,2) NOT NULL,
 TrangThai VARCHAR(30) NOT NULL, NguoiLapTaiKhoanID INT NOT NULL, NgayLap DATETIME2(0) NOT NULL, NgayKhachXacNhan DATETIME2(0) NULL,
 CONSTRAINT UX_BaoGiaDoiLich UNIQUE(ThayDoiLichID,PhienBan),
 CONSTRAINT FK_BaoGiaDoiLich_YeuCau FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID),
 CONSTRAINT FK_BaoGiaDoiLich_NguoiLap FOREIGN KEY(NguoiLapTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID));

IF OBJECT_ID('dbo.MaOtpDoiLich','U') IS NULL
CREATE TABLE dbo.MaOtpDoiLich(
 OtpID BIGINT IDENTITY PRIMARY KEY, DatTiecID INT NOT NULL, ThayDoiLichID INT NULL, MucDich VARCHAR(40) NOT NULL,
 SoDienThoaiHash CHAR(64) NOT NULL, IpHash CHAR(64) NOT NULL, MaHash NVARCHAR(255) NOT NULL,
 TokenXacThucHash CHAR(64) NULL, SoLanNhapSai INT NOT NULL CONSTRAINT DF_MaOtpDoiLich_Sai DEFAULT 0,
 NgayTao DATETIME2(0) NOT NULL, HetHanLuc DATETIME2(0) NOT NULL, XacThucLuc DATETIME2(0) NULL, DaDungLuc DATETIME2(0) NULL,
 CONSTRAINT CK_MaOtpDoiLich CHECK(HetHanLuc > NgayTao AND SoLanNhapSai BETWEEN 0 AND 5),
 CONSTRAINT FK_MaOtpDoiLich_DatTiec FOREIGN KEY(DatTiecID) REFERENCES dbo.DatTiec(DatTiecID),
 CONSTRAINT FK_MaOtpDoiLich_YeuCau FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_MaOtpDoiLich_Token')
 CREATE UNIQUE INDEX UX_MaOtpDoiLich_Token ON dbo.MaOtpDoiLich(TokenXacThucHash) WHERE TokenXacThucHash IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_MaOtpDoiLich_Rate')
 CREATE INDEX IX_MaOtpDoiLich_Rate ON dbo.MaOtpDoiLich(DatTiecID,MucDich,NgayTao);

-- Một số database cũ chưa chạy Add_PayOS_Payment_Tables.sql. Tạo đúng bảng hiện hành
-- theo cách cộng thêm để migration đổi lịch có thể chạy độc lập, không đụng giao dịch cũ.
IF OBJECT_ID('dbo.GiaoDichThanhToan','U') IS NULL
BEGIN
 CREATE TABLE dbo.GiaoDichThanhToan(
  GiaoDichID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GiaoDichThanhToan PRIMARY KEY,
  DatTiecID INT NOT NULL, OrderCode BIGINT NOT NULL, SoTienGiaoDich DECIMAL(18,2) NOT NULL,
  SoTienGhiNhan DECIMAL(18,2) NOT NULL, PhuongThuc NVARCHAR(50) NOT NULL CONSTRAINT DF_GiaoDichThanhToan_PhuongThuc DEFAULT N'VIETQR_PAYOS',
  TrangThai NVARCHAR(30) NOT NULL CONSTRAINT DF_GiaoDichThanhToan_TrangThai DEFAULT N'PENDING',
  NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_GiaoDichThanhToan_NgayTao DEFAULT SYSDATETIME(),
  NgayThanhToan DATETIME2(0) NULL, PayloadWebhook NVARCHAR(MAX) NULL,
  CONSTRAINT UQ_GiaoDichThanhToan_OrderCode UNIQUE(OrderCode),
  CONSTRAINT FK_GiaoDichThanhToan_DatTiec FOREIGN KEY(DatTiecID) REFERENCES dbo.DatTiec(DatTiecID));
 CREATE INDEX IX_GiaoDichThanhToan_DatTiecID ON dbo.GiaoDichThanhToan(DatTiecID);
 CREATE INDEX IX_GiaoDichThanhToan_TrangThai ON dbo.GiaoDichThanhToan(TrangThai);
END;

IF OBJECT_ID('dbo.GiuChoDoiLich','U') IS NULL
CREATE TABLE dbo.GiuChoDoiLich(
 GiuChoID INT IDENTITY PRIMARY KEY, ThayDoiLichID INT NOT NULL, LichSanhID INT NOT NULL, GiaoDichID BIGINT NULL,
 TrangThai VARCHAR(20) NOT NULL, BatDauLuc DATETIME2(0) NOT NULL, HetHanLuc DATETIME2(0) NOT NULL,
 GiaiPhongLuc DATETIME2(0) NULL, RowVersion ROWVERSION,
 CONSTRAINT CK_GiuChoDoiLich CHECK(HetHanLuc > BatDauLuc AND TrangThai IN('ACTIVE','CONSUMED','RELEASED','EXPIRED')),
 CONSTRAINT FK_GiuChoDoiLich_YeuCau FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID),
 CONSTRAINT FK_GiuChoDoiLich_Lich FOREIGN KEY(LichSanhID) REFERENCES dbo.LichSanh(LichSanhID));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_GiuChoDoiLich_LichSanh_Active')
 CREATE UNIQUE INDEX UX_GiuChoDoiLich_LichSanh_Active ON dbo.GiuChoDoiLich(LichSanhID) WHERE TrangThai='ACTIVE';

IF OBJECT_ID('dbo.LichSuTrangThaiDoiLich','U') IS NULL
CREATE TABLE dbo.LichSuTrangThaiDoiLich(
 LichSuID BIGINT IDENTITY PRIMARY KEY, ThayDoiLichID INT NOT NULL, TuTrangThai VARCHAR(40) NULL,
 DenTrangThai VARCHAR(40) NOT NULL, NguoiThucHienTaiKhoanID INT NULL, LoaiNguoiThucHien VARCHAR(30) NOT NULL,
 GhiChu NVARCHAR(1000) NULL, ThayDoiLuc DATETIME2(0) NOT NULL,
 CONSTRAINT FK_LichSuDoiLich_YeuCau FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID),
 CONSTRAINT FK_LichSuDoiLich_NguoiDung FOREIGN KEY(NguoiThucHienTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID));

IF OBJECT_ID('dbo.PhuLucHopDong','U') IS NULL
CREATE TABLE dbo.PhuLucHopDong(
 PhuLucID INT IDENTITY PRIMARY KEY, HopDongID INT NOT NULL, ThayDoiLichID INT NOT NULL, PhienBan INT NOT NULL,
 NoiDungDieuChinh NVARCHAR(MAX) NOT NULL, GiaTriTruoc DECIMAL(18,2) NOT NULL, GiaTriSau DECIMAL(18,2) NOT NULL,
 KhachXacNhanLuc DATETIME2(0) NULL, NguoiPheDuyetTaiKhoanID INT NOT NULL, HieuLucLuc DATETIME2(0) NOT NULL, TrangThai VARCHAR(30) NOT NULL,
 CONSTRAINT UX_PhuLucHopDong_YeuCau UNIQUE(ThayDoiLichID), CONSTRAINT UX_PhuLucHopDong_PhienBan UNIQUE(HopDongID,PhienBan),
 CONSTRAINT FK_PhuLucHopDong_HopDong FOREIGN KEY(HopDongID) REFERENCES dbo.HopDong(HopDongID),
 CONSTRAINT FK_PhuLucHopDong_YeuCau FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID),
 CONSTRAINT FK_PhuLucHopDong_NguoiDuyet FOREIGN KEY(NguoiPheDuyetTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID));

IF OBJECT_ID('dbo.HoanTienDoiLich','U') IS NULL
CREATE TABLE dbo.HoanTienDoiLich(
 HoanTienID INT IDENTITY PRIMARY KEY, ThayDoiLichID INT NOT NULL, SoTien DECIMAL(18,2) NOT NULL,
 TrangThai VARCHAR(30) NOT NULL, LyDo NVARCHAR(1000) NOT NULL, NguoiXuLyTaiKhoanID INT NULL,
 NgayTao DATETIME2(0) NOT NULL, NgayXuLy DATETIME2(0) NULL, BangChung NVARCHAR(2000) NULL,
 CONSTRAINT CK_HoanTienDoiLich CHECK(SoTien > 0),
 CONSTRAINT FK_HoanTienDoiLich_YeuCau FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID),
 CONSTRAINT FK_HoanTienDoiLich_NguoiXuLy FOREIGN KEY(NguoiXuLyTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID));

IF COL_LENGTH('dbo.GiaoDichThanhToan','ThayDoiLichID') IS NULL ALTER TABLE dbo.GiaoDichThanhToan ADD ThayDoiLichID INT NULL;
IF COL_LENGTH('dbo.GiaoDichThanhToan','LoaiGiaoDich') IS NULL ALTER TABLE dbo.GiaoDichThanhToan ADD LoaiGiaoDich VARCHAR(30) NULL;
IF COL_LENGTH('dbo.GiaoDichThanhToan','HetHanLuc') IS NULL ALTER TABLE dbo.GiaoDichThanhToan ADD HetHanLuc DATETIME2(0) NULL;
IF COL_LENGTH('dbo.GiaoDichThanhToan','DuLieuDoiSoat') IS NULL ALTER TABLE dbo.GiaoDichThanhToan ADD DuLieuDoiSoat NVARCHAR(MAX) NULL;
EXEC(N'UPDATE dbo.GiaoDichThanhToan SET LoaiGiaoDich=''DEPOSIT'' WHERE LoaiGiaoDich IS NULL;');
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GiaoDichThanhToan') AND name='LoaiGiaoDich' AND is_nullable=1)
 EXEC(N'ALTER TABLE dbo.GiaoDichThanhToan ALTER COLUMN LoaiGiaoDich VARCHAR(30) NOT NULL;');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_GiaoDichThanhToan_DoiLich')
 EXEC(N'ALTER TABLE dbo.GiaoDichThanhToan ADD CONSTRAINT FK_GiaoDichThanhToan_DoiLich FOREIGN KEY(ThayDoiLichID) REFERENCES dbo.ThayDoiLichTiec(ThayDoiLichID);');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_GiuChoDoiLich_GiaoDich')
 ALTER TABLE dbo.GiuChoDoiLich ADD CONSTRAINT FK_GiuChoDoiLich_GiaoDich FOREIGN KEY(GiaoDichID) REFERENCES dbo.GiaoDichThanhToan(GiaoDichID);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_GiaoDichThanhToan_DoiLich_HieuLuc')
 EXEC(N'CREATE UNIQUE INDEX UX_GiaoDichThanhToan_DoiLich_HieuLuc ON dbo.GiaoDichThanhToan(ThayDoiLichID,LoaiGiaoDich)
 WHERE ThayDoiLichID IS NOT NULL AND LoaiGiaoDich=''SCHEDULE_CHANGE'' AND TrangThai IN(''PENDING'',''PAID'');');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_GiuChoDoiLich_GiaoDich')
 CREATE UNIQUE INDEX UX_GiuChoDoiLich_GiaoDich ON dbo.GiuChoDoiLich(GiaoDichID) WHERE GiaoDichID IS NOT NULL;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_ThayDoiLichTiec_DatTiec_DangXuLy')
 CREATE UNIQUE INDEX UX_ThayDoiLichTiec_DatTiec_DangXuLy ON dbo.ThayDoiLichTiec(DatTiecID)
 WHERE TrangThaiID IN(1301,1304,1305,1306,1309);

COMMIT TRANSACTION;

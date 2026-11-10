SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.BannerTrangChu', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BannerTrangChu
    (
        BannerID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BannerTrangChu PRIMARY KEY,
        TieuDe NVARCHAR(150) NOT NULL,
        TieuDePhu NVARCHAR(150) NOT NULL,
        MoTa NVARCHAR(500) NOT NULL,
        NhanDieuHuong NVARCHAR(100) NOT NULL,
        DuongDanAnh NVARCHAR(300) NULL,
        MoTaAnh NVARCHAR(250) NOT NULL,
        LoaiHanhDongChinh VARCHAR(20) NOT NULL CONSTRAINT DF_BannerTrangChu_LoaiChinh DEFAULT 'NONE',
        NhanNutChinh NVARCHAR(80) NULL,
        LienKetChinh NVARCHAR(300) NULL,
        LoaiHanhDongPhu VARCHAR(20) NOT NULL CONSTRAINT DF_BannerTrangChu_LoaiPhu DEFAULT 'NONE',
        NhanNutPhu NVARCHAR(80) NULL,
        LienKetPhu NVARCHAR(300) NULL,
        ThuTu INT NOT NULL,
        DangHienThi BIT NOT NULL CONSTRAINT DF_BannerTrangChu_HienThi DEFAULT 0,
        NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_BannerTrangChu_NgayTao DEFAULT SYSDATETIME(),
        NgayCapNhat DATETIME2(0) NULL,
        NguoiTaoID INT NULL,
        NguoiCapNhatID INT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_BannerTrangChu_ThuTu CHECK (ThuTu > 0),
        CONSTRAINT CK_BannerTrangChu_LoaiChinh CHECK (LoaiHanhDongChinh IN ('NONE','INTERNAL','SECTION','AI')),
        CONSTRAINT CK_BannerTrangChu_LoaiPhu CHECK (LoaiHanhDongPhu IN ('NONE','INTERNAL','SECTION','AI')),
        CONSTRAINT FK_BannerTrangChu_NguoiTao FOREIGN KEY (NguoiTaoID) REFERENCES dbo.TaiKhoan(TaiKhoanID),
        CONSTRAINT FK_BannerTrangChu_NguoiCapNhat FOREIGN KEY (NguoiCapNhatID) REFERENCES dbo.TaiKhoan(TaiKhoanID)
    );
    CREATE INDEX IX_BannerTrangChu_ThuTu ON dbo.BannerTrangChu(ThuTu, BannerID);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE DuongDanAnh = N'/images/home/hero-ballroom-8k.jpg')
    INSERT dbo.BannerTrangChu(TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,NhanNutChinh,LienKetChinh,LoaiHanhDongPhu,NhanNutPhu,LienKetPhu,ThuTu,DangHienThi)
    VALUES(N'Kiệt Tác Sảnh Tiệc Hoàng Gia',N'Khởi Đầu Trăm Năm Viên Mãn',N'9 đại sảnh phong cách châu Âu ven sông Bến Vân Đồn, mang đến ngày cưới trọn vẹn và đẳng cấp.',N'09 Đại Sảnh Hoàng Gia',N'/images/home/hero-ballroom-8k.jpg',N'Đại sảnh tiệc cưới hoàng gia Riverside Palace','SECTION',N'Khám phá 9 sảnh tiệc',N'#danh-sach-sanh','AI',N'Tư vấn AI Concierge',NULL,1,1);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE DuongDanAnh = N'/images/home/hero-riverfront-8k.jpg')
    INSERT dbo.BannerTrangChu(TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,NhanNutChinh,LienKetChinh,LoaiHanhDongPhu,NhanNutPhu,LienKetPhu,ThuTu,DangHienThi)
    VALUES(N'Thề Nguyện Lãng Mạn Ven Sông',N'Hoàng Hôn Sài Gòn Độc Bản',N'Không gian lãng mạn cùng ánh hoàng hôn thi vị, tôn vinh khoảnh khắc hẹn ước trăm năm thiêng liêng.',N'Lễ Đường Ven Sông',N'/images/home/hero-riverfront-8k.jpg',N'Lễ đường cưới ven sông lãng mạn hoàng hôn Riverside Palace','INTERNAL',N'Thiết kế tiệc trong mơ',N'/Recommendation','SECTION',N'Xem danh sách sảnh',N'#danh-sach-sanh',2,1);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE DuongDanAnh = N'/images/home/hero-cuisine-8k.jpg')
    INSERT dbo.BannerTrangChu(TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,NhanNutChinh,LienKetChinh,LoaiHanhDongPhu,NhanNutPhu,LienKetPhu,ThuTu,DangHienThi)
    VALUES(N'Mỹ Vị Yến Tiệc Đỉnh Cao',N'Thăng Hoa Cùng Vị Giác',N'Hơn 300 món Fusion Á - Âu thượng hạng được chế tác tinh tế bởi đội ngũ bếp trưởng chuyên nghiệp.',N'Ẩm Thực Chuẩn 5 Sao',N'/images/home/hero-cuisine-8k.jpg',N'Đỉnh cao ẩm thực yến tiệc 5 sao Riverside Palace','INTERNAL',N'Khám phá thực đơn',N'/Menus','AI',N'Gợi ý thực đơn cùng AI',NULL,3,1);

-- Repair only default rows accidentally imported with a non-UTF-8 sqlcmd code page.
IF EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE DuongDanAnh = N'/images/home/hero-ballroom-8k.jpg' AND TieuDe LIKE N'%Ã%')
    UPDATE dbo.BannerTrangChu SET TieuDe=N'Kiệt Tác Sảnh Tiệc Hoàng Gia',TieuDePhu=N'Khởi Đầu Trăm Năm Viên Mãn',MoTa=N'9 đại sảnh phong cách châu Âu ven sông Bến Vân Đồn, mang đến ngày cưới trọn vẹn và đẳng cấp.',NhanDieuHuong=N'09 Đại Sảnh Hoàng Gia',MoTaAnh=N'Đại sảnh tiệc cưới hoàng gia Riverside Palace',NhanNutChinh=N'Khám phá 9 sảnh tiệc',NhanNutPhu=N'Tư vấn AI Concierge' WHERE DuongDanAnh=N'/images/home/hero-ballroom-8k.jpg';
IF EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE DuongDanAnh = N'/images/home/hero-riverfront-8k.jpg' AND TieuDe LIKE N'%Ã%')
    UPDATE dbo.BannerTrangChu SET TieuDe=N'Thề Nguyện Lãng Mạn Ven Sông',TieuDePhu=N'Hoàng Hôn Sài Gòn Độc Bản',MoTa=N'Không gian lãng mạn cùng ánh hoàng hôn thi vị, tôn vinh khoảnh khắc hẹn ước trăm năm thiêng liêng.',NhanDieuHuong=N'Lễ Đường Ven Sông',MoTaAnh=N'Lễ đường cưới ven sông lãng mạn hoàng hôn Riverside Palace',NhanNutChinh=N'Thiết kế tiệc trong mơ',NhanNutPhu=N'Xem danh sách sảnh' WHERE DuongDanAnh=N'/images/home/hero-riverfront-8k.jpg';
IF EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE DuongDanAnh = N'/images/home/hero-cuisine-8k.jpg' AND (TieuDe LIKE N'%Ã%' OR TieuDe LIKE N'%»%' OR TieuDe LIKE N'%Ä%'))
    UPDATE dbo.BannerTrangChu SET TieuDe=N'Mỹ Vị Yến Tiệc Đỉnh Cao',TieuDePhu=N'Thăng Hoa Cùng Vị Giác',MoTa=N'Hơn 300 món Fusion Á - Âu thượng hạng được chế tác tinh tế bởi đội ngũ bếp trưởng chuyên nghiệp.',NhanDieuHuong=N'Ẩm Thực Chuẩn 5 Sao',MoTaAnh=N'Đỉnh cao ẩm thực yến tiệc 5 sao Riverside Palace',NhanNutChinh=N'Khám phá thực đơn',NhanNutPhu=N'Gợi ý thực đơn cùng AI' WHERE DuongDanAnh=N'/images/home/hero-cuisine-8k.jpg';

COMMIT TRANSACTION;

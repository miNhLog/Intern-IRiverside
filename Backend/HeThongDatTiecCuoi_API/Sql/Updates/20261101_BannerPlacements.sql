SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.BannerTrangChu', N'ViTri') IS NULL
BEGIN
    ALTER TABLE dbo.BannerTrangChu
        ADD ViTri VARCHAR(30) NOT NULL
            CONSTRAINT DF_BannerTrangChu_ViTri DEFAULT 'HOME';
END;
GO

UPDATE dbo.BannerTrangChu SET ViTri = 'HOME' WHERE ViTri IS NULL OR LTRIM(RTRIM(ViTri)) = '';

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_BannerTrangChu_ViTri')
    ALTER TABLE dbo.BannerTrangChu ADD CONSTRAINT CK_BannerTrangChu_ViTri
        CHECK (ViTri IN ('HOME','HALLS','MENUS','DECOR','RECOMMENDATION','MY_BOOKING'));

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BannerTrangChu') AND name = N'IX_BannerTrangChu_ThuTu')
    DROP INDEX IX_BannerTrangChu_ThuTu ON dbo.BannerTrangChu;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BannerTrangChu') AND name = N'IX_BannerTrangChu_ViTri_ThuTu')
    CREATE INDEX IX_BannerTrangChu_ViTri_ThuTu ON dbo.BannerTrangChu(ViTri, ThuTu, BannerID);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE ViTri = 'HALLS')
    INSERT dbo.BannerTrangChu(ViTri,TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,LoaiHanhDongPhu,ThuTu,DangHienThi)
    VALUES('HALLS',N'Không Gian Đại Sảnh Đẳng Cấp',N'Dấu Ấn Cho Ngày Trọng Đại',N'Không gian tiệc cưới sang trọng với ánh sáng, hoa tươi và bàn tiệc được chuẩn bị chỉn chu.',N'Banner Sảnh Tiệc',N'/images/page-banners/halls.jpg',N'Đại sảnh tiệc cưới sang trọng với hoa tươi và đèn chùm','NONE','NONE',1,1);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE ViTri = 'MENUS')
    INSERT dbo.BannerTrangChu(ViTri,TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,LoaiHanhDongPhu,ThuTu,DangHienThi)
    VALUES('MENUS',N'Tinh Hoa Ẩm Thực Yến Tiệc',N'Trọn Vị Trong Từng Khoảnh Khắc',N'Bàn tiệc được trình bày tinh tế, cân bằng hương vị và trải nghiệm của từng vị khách.',N'Banner Thực Đơn',N'/images/page-banners/menus.jpg',N'Bàn tiệc cưới cao cấp được bày trí tinh tế','NONE','NONE',1,1);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE ViTri = 'DECOR')
    INSERT dbo.BannerTrangChu(ViTri,TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,LoaiHanhDongPhu,ThuTu,DangHienThi)
    VALUES('DECOR',N'Không Gian Cưới Mang Dấu Ấn Riêng',N'Từng Chi Tiết Đều Có Câu Chuyện',N'Phong cách trang trí hoa thanh lịch giúp biến ý tưởng của đôi uyên ương thành không gian giàu cảm xúc.',N'Banner Trang Trí',N'/images/page-banners/decor.jpg',N'Không gian trang trí tiệc cưới với hoa trắng thanh lịch','NONE','NONE',1,1);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE ViTri = 'RECOMMENDATION')
    INSERT dbo.BannerTrangChu(ViTri,TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,LoaiHanhDongPhu,ThuTu,DangHienThi)
    VALUES('RECOMMENDATION',N'Thiết Kế Tiệc Cưới Trong Mơ',N'Gợi Ý Phù Hợp Với Riêng Bạn',N'Khám phá cấu hình sảnh, thực đơn và trang trí phù hợp với phong cách cùng ngân sách dự kiến.',N'Banner Gợi Ý Tiệc',N'/images/page-banners/recommendation.jpg',N'Cặp đôi nắm tay trong nghi lễ cưới trang trọng','NONE','NONE',1,1);

IF NOT EXISTS (SELECT 1 FROM dbo.BannerTrangChu WHERE ViTri = 'MY_BOOKING')
    INSERT dbo.BannerTrangChu(ViTri,TieuDe,TieuDePhu,MoTa,NhanDieuHuong,DuongDanAnh,MoTaAnh,LoaiHanhDongChinh,LoaiHanhDongPhu,ThuTu,DangHienThi)
    VALUES('MY_BOOKING',N'Hành Trình Ngày Cưới Của Bạn',N'Mọi Kế Hoạch Trong Một Nơi',N'Theo dõi thông tin đặt tiệc, tiến độ chuẩn bị và những cột mốc quan trọng của ngày chung đôi.',N'Banner Tiệc Của Tôi',N'/images/page-banners/my-booking.jpg',N'Cặp đôi hạnh phúc trong ngày cưới','NONE','NONE',1,1);

COMMIT TRANSACTION;

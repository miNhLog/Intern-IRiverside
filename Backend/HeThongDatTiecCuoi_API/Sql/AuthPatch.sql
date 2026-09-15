USE RiversidePalaceDB;
GO

-- Vai trò bắt buộc, có thể chạy lại an toàn.
IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Admin')
    INSERT INTO VaiTro (TenVaiTro) VALUES (N'Admin');
IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Nhân viên tư vấn')
    INSERT INTO VaiTro (TenVaiTro) VALUES (N'Nhân viên tư vấn');
IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Khách hàng')
    INSERT INTO VaiTro (TenVaiTro) VALUES (N'Khách hàng');
GO

-- Kiểm tra dữ liệu trùng trước khi tạo UNIQUE index.
IF EXISTS (
    SELECT SoDienThoai
    FROM KhachHang
    GROUP BY SoDienThoai
    HAVING COUNT(*) > 1
)
    THROW 51000, N'KhachHang đang có số điện thoại trùng. Hãy làm sạch dữ liệu trước.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_KhachHang_SoDienThoai' AND object_id = OBJECT_ID(N'KhachHang'))
BEGIN
    CREATE UNIQUE INDEX UX_KhachHang_SoDienThoai ON KhachHang(SoDienThoai);
END
GO

IF EXISTS (
    SELECT SoDienThoai
    FROM NhanVien
    WHERE SoDienThoai IS NOT NULL
    GROUP BY SoDienThoai
    HAVING COUNT(*) > 1
)
    THROW 51001, N'NhanVien đang có số điện thoại trùng. Hãy làm sạch dữ liệu trước.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NhanVien_SoDienThoai' AND object_id = OBJECT_ID(N'NhanVien'))
BEGIN
    CREATE UNIQUE INDEX UX_NhanVien_SoDienThoai
        ON NhanVien(SoDienThoai)
        WHERE SoDienThoai IS NOT NULL;
END
GO

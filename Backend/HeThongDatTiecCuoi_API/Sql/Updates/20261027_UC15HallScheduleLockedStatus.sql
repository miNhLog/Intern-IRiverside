-- UC15 reference-data upgrade: bổ sung trạng thái HALL_SCHEDULE 303 LOCKED.
-- Chỉ chạy trên database đã được Owner phê duyệt; không tự chạy khi khởi động.
-- Idempotent: chạy lại nhiều lần không tạo trùng và không sửa dữ liệu hiện có.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM dbo.TrangThai
    WHERE NhomTrangThai = N'HALL_SCHEDULE' AND MaTrangThai = N'LOCKED')
BEGIN
    -- TrangThaiID không phải IDENTITY: mã 303 phải còn trống mới chèn được an toàn.
    IF EXISTS (SELECT 1 FROM dbo.TrangThai WHERE TrangThaiID = 303)
        THROW 50000, N'TrangThaiID 303 đang được dùng cho trạng thái khác. Cần rà soát thủ công trước khi thêm HALL_SCHEDULE/LOCKED.', 1;

    INSERT INTO dbo.TrangThai (TrangThaiID, MaTrangThai, TenTrangThai, NhomTrangThai, MoTa)
    VALUES (303, N'LOCKED', N'Đã khóa', N'HALL_SCHEDULE', N'Ca tổ chức bị khóa vận hành, không tự động giải phóng');
END

COMMIT;

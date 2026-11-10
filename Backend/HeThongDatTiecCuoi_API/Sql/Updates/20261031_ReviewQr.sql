/* Review QR hardening. Safe to run repeatedly on SQL Server. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DanhGia_ChuTiec_MaQR' AND object_id = OBJECT_ID('dbo.DanhGia'))
BEGIN
    CREATE UNIQUE INDEX UX_DanhGia_ChuTiec_MaQR
        ON dbo.DanhGia(MaQRDanhGiaID)
        WHERE LoaiNguoiDanhGia = N'Chủ tiệc';
END;
GO

/* Create only still-valid missing QR rows. Tokens are cryptographically random UUIDs.
   Application backfill is preferred because it uses the canonical shift parser. */

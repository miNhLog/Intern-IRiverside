/* UC22 review feedback hardening. Safe to run repeatedly on SQL Server.
   Adds the sixth guest criterion and the hashed anonymous-session key used for
   duplicate suppression. Historical rows stay valid: both columns are nullable.
   Atomic; XACT_ABORT on. Statements referencing newly added columns run in dynamic
   SQL so SQL Server does not bind them too early in the batch. */
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.DanhGia', 'DiemVeSinh') IS NULL
        EXEC sp_executesql N'ALTER TABLE dbo.DanhGia ADD DiemVeSinh INT NULL;';
    IF COL_LENGTH('dbo.DanhGia', 'PhienHash') IS NULL
        EXEC sp_executesql N'ALTER TABLE dbo.DanhGia ADD PhienHash VARCHAR(64) NULL;';

    IF OBJECT_ID('dbo.CK_DanhGia_DiemVeSinh', 'C') IS NULL
        EXEC sp_executesql N'ALTER TABLE dbo.DanhGia ADD CONSTRAINT CK_DanhGia_DiemVeSinh
            CHECK (DiemVeSinh IS NULL OR DiemVeSinh BETWEEN 1 AND 5);';

    /* One guest review per QR and anonymous browser session. Filtered so host reviews
       and guest rows written before this change do not block the index. */
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DanhGia_KhachMoi_MaQR_Phien'
        AND object_id = OBJECT_ID('dbo.DanhGia'))
        EXEC sp_executesql N'CREATE UNIQUE INDEX UX_DanhGia_KhachMoi_MaQR_Phien
            ON dbo.DanhGia (MaQRDanhGiaID, PhienHash)
            WHERE PhienHash IS NOT NULL AND LoaiNguoiDanhGia = N''Khách mời'';';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

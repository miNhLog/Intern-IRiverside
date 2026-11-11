-- UC18 structured billed/spare table counts. Apply only with owner approval; never run at startup.
-- Nullable legacy values are intentionally preserved; no default, backfill, or seed data.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.DatTiec', N'SoBanTinhTien') IS NULL
    ALTER TABLE dbo.DatTiec ADD SoBanTinhTien int NULL;

IF COL_LENGTH(N'dbo.DatTiec', N'SoBanDuPhong') IS NULL
    ALTER TABLE dbo.DatTiec ADD SoBanDuPhong int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.DatTiec') AND name = N'CK_DatTiec_SoBanCoCauTruc')
    EXEC(N'ALTER TABLE dbo.DatTiec ADD CONSTRAINT CK_DatTiec_SoBanCoCauTruc CHECK (
        ([SoBanTinhTien] IS NULL AND [SoBanDuPhong] IS NULL) OR
        ([SoBanTinhTien] IS NOT NULL AND [SoBanDuPhong] IS NOT NULL AND
         [SoBanTinhTien] > 0 AND [SoBanDuPhong] >= 0 AND [SoBanDuPhong] < [SoBanTinhTien]))');

COMMIT;

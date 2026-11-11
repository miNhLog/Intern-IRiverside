-- UC18 custom-menu selection snapshot. Apply only with owner approval; never run at startup.
-- Nullable legacy values are intentionally preserved; no backfill.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.DatTiec', N'ThucDonTuyChinhSnapshotJson') IS NULL
    ALTER TABLE dbo.DatTiec ADD ThucDonTuyChinhSnapshotJson nvarchar(max) NULL;

COMMIT;

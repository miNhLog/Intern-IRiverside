-- UC17 additive upgrade. Apply only to an explicitly approved database after backup.
-- No USE, no backfill, no status updates. Never execute automatically at startup.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.HopDong', N'SoTienCocCamKet') IS NULL
    ALTER TABLE dbo.HopDong ADD SoTienCocCamKet DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.ThanhToan', N'RequestId') IS NULL
    ALTER TABLE dbo.ThanhToan ADD RequestId UNIQUEIDENTIFIER NULL;
-- Dynamic SQL permits adding and indexing the column in the same upgrade batch.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ThanhToan') AND name = N'UX_ThanhToan_RequestId')
    EXEC(N'CREATE UNIQUE INDEX UX_ThanhToan_RequestId ON dbo.ThanhToan(RequestId) WHERE RequestId IS NOT NULL');
COMMIT;

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NhatKyThaoTac') AND name = N'UX_NhatKyThaoTac_Logout_CorrelationId')
    DROP INDEX UX_NhatKyThaoTac_Logout_CorrelationId ON dbo.NhatKyThaoTac;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NhatKyThaoTac') AND name = N'IX_NhatKyThaoTac_HanhDong_ThoiGian')
    DROP INDEX IX_NhatKyThaoTac_HanhDong_ThoiGian ON dbo.NhatKyThaoTac;

ALTER TABLE dbo.NhatKyThaoTac ALTER COLUMN HanhDong varchar(50) NOT NULL;

CREATE INDEX IX_NhatKyThaoTac_HanhDong_ThoiGian
    ON dbo.NhatKyThaoTac(HanhDong, ThoiGian DESC);
CREATE UNIQUE INDEX UX_NhatKyThaoTac_Logout_CorrelationId
    ON dbo.NhatKyThaoTac(CorrelationId)
    WHERE HanhDong = 'LOGOUT' AND CorrelationId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NhatKyThaoTac') AND name = N'IX_NhatKyThaoTac_DoiTuong_ID_ThoiGian')
    CREATE INDEX IX_NhatKyThaoTac_DoiTuong_ID_ThoiGian
        ON dbo.NhatKyThaoTac(DoiTuong, DoiTuongID, ThoiGian DESC);

COMMIT TRANSACTION;

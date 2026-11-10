SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.NhatKyThaoTac', N'DiaChiIP') IS NULL
    ALTER TABLE dbo.NhatKyThaoTac ADD DiaChiIP nvarchar(45) NULL;
IF COL_LENGTH(N'dbo.NhatKyThaoTac', N'UserAgent') IS NULL
    ALTER TABLE dbo.NhatKyThaoTac ADD UserAgent nvarchar(500) NULL;
IF COL_LENGTH(N'dbo.NhatKyThaoTac', N'PhuongThucXacThuc') IS NULL
    ALTER TABLE dbo.NhatKyThaoTac ADD PhuongThucXacThuc varchar(30) NULL;
IF COL_LENGTH(N'dbo.NhatKyThaoTac', N'KetQua') IS NULL
    ALTER TABLE dbo.NhatKyThaoTac ADD KetQua varchar(20) NULL;
IF COL_LENGTH(N'dbo.NhatKyThaoTac', N'CorrelationId') IS NULL
    ALTER TABLE dbo.NhatKyThaoTac ADD CorrelationId nvarchar(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NhatKyThaoTac') AND name = N'UX_NhatKyThaoTac_Logout_CorrelationId')
    EXEC(N'CREATE UNIQUE INDEX UX_NhatKyThaoTac_Logout_CorrelationId
        ON dbo.NhatKyThaoTac(CorrelationId)
        WHERE HanhDong = ''LOGOUT'' AND CorrelationId IS NOT NULL;');

COMMIT TRANSACTION;

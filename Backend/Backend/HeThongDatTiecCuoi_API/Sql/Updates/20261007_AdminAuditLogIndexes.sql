SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.NhatKyThaoTac', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NhatKyThaoTac') AND name = N'IX_NhatKyThaoTac_ThoiGian_ID')
        CREATE INDEX IX_NhatKyThaoTac_ThoiGian_ID ON dbo.NhatKyThaoTac(ThoiGian DESC, NhatKyThaoTacID DESC);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NhatKyThaoTac') AND name = N'IX_NhatKyThaoTac_HanhDong_ThoiGian')
        CREATE INDEX IX_NhatKyThaoTac_HanhDong_ThoiGian ON dbo.NhatKyThaoTac(HanhDong, ThoiGian DESC);
END;

COMMIT TRANSACTION;

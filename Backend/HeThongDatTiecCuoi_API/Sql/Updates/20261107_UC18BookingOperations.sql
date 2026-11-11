-- UC18 additive operation and refund-confirmation tables.
-- Apply only to an explicitly approved database. No seed, backfill, or automatic execution.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ThaoTacDatTiec', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ThaoTacDatTiec
    (
        RequestId UNIQUEIDENTIFIER NOT NULL,
        DatTiecID INT NOT NULL,
        HopDongID INT NULL,
        NguoiThucHienTaiKhoanID INT NOT NULL,
        LoaiThaoTac VARCHAR(20) NOT NULL,
        PayloadHash VARCHAR(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        RecordedAtUtc DATETIME2(7) NOT NULL,
        PolicyVersion VARCHAR(50) NULL,
        ManagerAgreementReference NVARCHAR(300) NULL,
        SnapshotJson NVARCHAR(MAX) NULL,
        SignedPriceDelta DECIMAL(18,2) NOT NULL,
        FeeAmount DECIMAL(18,2) NOT NULL,
        EffectiveTotal DECIMAL(18,2) NULL,
        RetainedDeposit DECIMAL(18,2) NOT NULL,
        PrincipalRefundDue DECIMAL(18,2) NOT NULL,
        DepositPrincipalPortion DECIMAL(18,2) NOT NULL,
        CompensationDue DECIMAL(18,2) NOT NULL,
        CONSTRAINT PK_ThaoTacDatTiec PRIMARY KEY (RequestId),
        CONSTRAINT FK_ThaoTacDatTiec_DatTiec FOREIGN KEY (DatTiecID) REFERENCES dbo.DatTiec(DatTiecID),
        CONSTRAINT FK_ThaoTacDatTiec_HopDong FOREIGN KEY (HopDongID) REFERENCES dbo.HopDong(HopDongID),
        CONSTRAINT FK_ThaoTacDatTiec_TaiKhoan FOREIGN KEY (NguoiThucHienTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID),
        CONSTRAINT CK_ThaoTacDatTiec_RequestId CHECK
            (RequestId <> CONVERT(UNIQUEIDENTIFIER, '00000000-0000-0000-0000-000000000000')),
        CONSTRAINT CK_ThaoTacDatTiec_LoaiThaoTac CHECK (LoaiThaoTac IN ('RESCHEDULE', 'MENU_CHANGE', 'CANCEL', 'HANDOFF', 'PRIOR_REFUND')),
        CONSTRAINT CK_ThaoTacDatTiec_PayloadHash CHECK
            (DATALENGTH(PayloadHash) = 64 AND PayloadHash NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT CK_ThaoTacDatTiec_SnapshotJson CHECK (SnapshotJson IS NULL OR ISJSON(SnapshotJson) = 1),
        CONSTRAINT CK_ThaoTacDatTiec_FeeAmount CHECK (FeeAmount >= 0),
        CONSTRAINT CK_ThaoTacDatTiec_EffectiveTotal CHECK (EffectiveTotal IS NULL OR EffectiveTotal >= 0),
        CONSTRAINT CK_ThaoTacDatTiec_RetainedDeposit CHECK (RetainedDeposit >= 0),
        CONSTRAINT CK_ThaoTacDatTiec_PrincipalRefundDue CHECK (PrincipalRefundDue >= 0),
        CONSTRAINT CK_ThaoTacDatTiec_DepositPrincipalPortion CHECK
            (DepositPrincipalPortion >= 0 AND DepositPrincipalPortion <= PrincipalRefundDue),
        CONSTRAINT CK_ThaoTacDatTiec_CompensationDue CHECK (CompensationDue >= 0)
    );
END;

IF OBJECT_ID(N'dbo.XacNhanHoanTien', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.XacNhanHoanTien
    (
        RequestId UNIQUEIDENTIFIER NOT NULL,
        ThaoTacRequestId UNIQUEIDENTIFIER NOT NULL,
        NguoiXacNhanTaiKhoanID INT NOT NULL,
        PayloadHash VARCHAR(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        ExternalEvidenceReference NVARCHAR(300) NOT NULL,
        ConfirmedAtUtc DATETIME2(7) NOT NULL,
        PrincipalPaid DECIMAL(18,2) NOT NULL,
        DepositPrincipalPaid DECIMAL(18,2) NOT NULL,
        CompensationPaid DECIMAL(18,2) NOT NULL,
        CONSTRAINT PK_XacNhanHoanTien PRIMARY KEY (RequestId),
        CONSTRAINT FK_XacNhanHoanTien_ThaoTacDatTiec FOREIGN KEY (ThaoTacRequestId) REFERENCES dbo.ThaoTacDatTiec(RequestId),
        CONSTRAINT FK_XacNhanHoanTien_TaiKhoan FOREIGN KEY (NguoiXacNhanTaiKhoanID) REFERENCES dbo.TaiKhoan(TaiKhoanID),
        CONSTRAINT CK_XacNhanHoanTien_RequestId CHECK
            (RequestId <> CONVERT(UNIQUEIDENTIFIER, '00000000-0000-0000-0000-000000000000')),
        CONSTRAINT CK_XacNhanHoanTien_PayloadHash CHECK
            (DATALENGTH(PayloadHash) = 64 AND PayloadHash NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT CK_XacNhanHoanTien_PrincipalPaid CHECK (PrincipalPaid >= 0),
        CONSTRAINT CK_XacNhanHoanTien_DepositPrincipalPaid CHECK
            (DepositPrincipalPaid >= 0 AND DepositPrincipalPaid <= PrincipalPaid),
        CONSTRAINT CK_XacNhanHoanTien_CompensationPaid CHECK (CompensationPaid >= 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ThaoTacDatTiec') AND name = N'IX_ThaoTacDatTiec_DatTiec_LichSu')
    CREATE INDEX IX_ThaoTacDatTiec_DatTiec_LichSu ON dbo.ThaoTacDatTiec(DatTiecID, RecordedAtUtc);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ThaoTacDatTiec') AND name = N'IX_ThaoTacDatTiec_HopDong_LichSu')
    CREATE INDEX IX_ThaoTacDatTiec_HopDong_LichSu ON dbo.ThaoTacDatTiec(HopDongID, RecordedAtUtc) WHERE HopDongID IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.XacNhanHoanTien') AND name = N'IX_XacNhanHoanTien_ThaoTac_TongTien')
    CREATE INDEX IX_XacNhanHoanTien_ThaoTac_TongTien ON dbo.XacNhanHoanTien(ThaoTacRequestId)
        INCLUDE (PrincipalPaid, DepositPrincipalPaid, CompensationPaid);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.XacNhanHoanTien') AND name = N'UX_XacNhanHoanTien_ExternalEvidenceReference')
    CREATE UNIQUE INDEX UX_XacNhanHoanTien_ExternalEvidenceReference ON dbo.XacNhanHoanTien(ExternalEvidenceReference);

COMMIT;

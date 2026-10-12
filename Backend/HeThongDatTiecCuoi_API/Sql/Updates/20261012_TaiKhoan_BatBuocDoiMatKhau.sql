SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF DB_NAME() <> N'IRiversidePalaceDB'
BEGIN
    ;THROW 51000, N'Phai ket noi den database IRiversidePalaceDB.', 1;
END;

IF OBJECT_ID(N'dbo.TaiKhoan', N'U') IS NULL
BEGIN
    ;THROW 51001, N'Khong tim thay bang dbo.TaiKhoan.', 1;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ColumnId INT;
    DECLARE @TypeName SYSNAME;
    DECLARE @IsNullable BIT;
    DECLARE @DefaultDefinition NVARCHAR(MAX);

    SELECT
        @ColumnId = columnInfo.column_id,
        @TypeName = TYPE_NAME(columnInfo.user_type_id),
        @IsNullable = columnInfo.is_nullable,
        @DefaultDefinition = defaultConstraint.definition
    FROM sys.columns AS columnInfo
    LEFT JOIN sys.default_constraints AS defaultConstraint
        ON defaultConstraint.parent_object_id = columnInfo.object_id
        AND defaultConstraint.parent_column_id = columnInfo.column_id
    WHERE columnInfo.object_id = OBJECT_ID(N'dbo.TaiKhoan')
        AND columnInfo.name = N'BatBuocDoiMatKhau';

    IF @ColumnId IS NULL
    BEGIN
        ALTER TABLE dbo.TaiKhoan
            ADD BatBuocDoiMatKhau BIT NOT NULL
                CONSTRAINT DF_TaiKhoan_BatBuocDoiMatKhau DEFAULT (0) WITH VALUES;
    END
    ELSE
    BEGIN
        IF @TypeName <> N'bit' OR @IsNullable <> 0
        BEGIN
            ;THROW 51002, N'Cot dbo.TaiKhoan.BatBuocDoiMatKhau phai co kieu BIT NOT NULL.', 1;
        END;

        IF @DefaultDefinition IS NULL
        BEGIN
            ;THROW 51003, N'Cot dbo.TaiKhoan.BatBuocDoiMatKhau chua co DEFAULT constraint.', 1;
        END;

        DECLARE @NormalizedDefault NVARCHAR(MAX) = UPPER(@DefaultDefinition);
        SET @NormalizedDefault = REPLACE(@NormalizedDefault, N'(', N'');
        SET @NormalizedDefault = REPLACE(@NormalizedDefault, N')', N'');
        SET @NormalizedDefault = REPLACE(@NormalizedDefault, N' ', N'');
        SET @NormalizedDefault = REPLACE(@NormalizedDefault, NCHAR(9), N'');
        SET @NormalizedDefault = REPLACE(@NormalizedDefault, NCHAR(10), N'');
        SET @NormalizedDefault = REPLACE(@NormalizedDefault, NCHAR(13), N'');

        IF @NormalizedDefault <> N'0'
        BEGIN
            ;THROW 51004, N'DEFAULT cua dbo.TaiKhoan.BatBuocDoiMatKhau phai co gia tri 0.', 1;
        END;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO

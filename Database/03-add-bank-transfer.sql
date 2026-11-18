
USE [TechPartsStore];
GO
SET XACT_ABORT ON;

BEGIN TRY
    IF DB_NAME() <> N'TechPartsStore'
        THROW 50003, N'Hãy kết nối đúng CSDL TechPartsStore trước khi chạy script.', 1;

    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
        THROW 50004, N'Chưa có bảng Order. Hãy chạy 01-setup.sql trước.', 1;

    IF COL_LENGTH(N'dbo.[Order]', N'PaymentMethod') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE [dbo].[Order]
            ADD [PaymentMethod] NVARCHAR(20) NOT NULL
                CONSTRAINT [DF_Order_PaymentMethod] DEFAULT (N''COD'') WITH VALUES;');
    END
    ELSE
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns
            WHERE [object_id] = OBJECT_ID(N'dbo.[Order]')
              AND [name] = N'PaymentMethod'
              AND [system_type_id] = TYPE_ID(N'nvarchar')
              AND [max_length] = 40 AND [is_nullable] = 0 AND [is_computed] = 0
        )
            THROW 50005, N'PaymentMethod đã tồn tại nhưng không phải NVARCHAR(20) NOT NULL. Hãy đối chiếu cấu trúc trước khi tiếp tục.', 1;

        IF NOT EXISTS (
            SELECT 1 FROM sys.default_constraints d
            INNER JOIN sys.columns c
                ON c.[object_id] = d.[parent_object_id]
               AND c.[column_id] = d.[parent_column_id]
            WHERE c.[object_id] = OBJECT_ID(N'dbo.[Order]')
              AND c.[name] = N'PaymentMethod'
        )
            EXEC(N'ALTER TABLE [dbo].[Order]
                ADD CONSTRAINT [DF_Order_PaymentMethod] DEFAULT (N''COD'') FOR [PaymentMethod];');
    END;

    EXEC(N'IF EXISTS (
        SELECT 1 FROM [dbo].[Order]
        WHERE [PaymentMethod] IS NULL
           OR [PaymentMethod] NOT IN (N''COD'', N''BankTransfer'')
    )
        THROW 50006, N''Có PaymentMethod không hợp lệ. Script không thay đổi dữ liệu; hãy kiểm tra các đơn hiện có.'', 1;');

    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'dbo.[Order]')
          AND [name] = N'CK_Order_PaymentMethod'
    )
        EXEC(N'ALTER TABLE [dbo].[Order] WITH CHECK
            ADD CONSTRAINT [CK_Order_PaymentMethod]
            CHECK ([PaymentMethod] IN (N''COD'', N''BankTransfer''));');

    EXEC(N'ALTER TABLE [dbo].[Order] WITH CHECK CHECK CONSTRAINT [CK_Order_PaymentMethod];');

    COMMIT TRANSACTION;
    PRINT N'Đã bổ sung phương thức COD/BankTransfer. Dữ liệu cũ được giữ nguyên.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

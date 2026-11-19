
USE [TechPartsStore];
GO
SET XACT_ABORT ON;

BEGIN TRY
    IF DB_NAME() <> N'TechPartsStore'
        THROW 50010, N'Hãy kết nối đúng CSDL TechPartsStore trước khi chạy script.', 1;

    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
        THROW 50011, N'Chưa có bảng Order. Hãy chạy 01-setup.sql trước.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE [object_id] = OBJECT_ID(N'dbo.[Order]')
          AND [name] = N'PaymentStatus'
          AND [system_type_id] = TYPE_ID(N'nvarchar')
          AND [max_length] = 40 AND [is_nullable] = 0 AND [is_computed] = 0
    )
        THROW 50012, N'PaymentStatus phải là NVARCHAR(20) NOT NULL. Hãy đối chiếu cấu trúc bảng Order trước khi tiếp tục.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE [object_id] = OBJECT_ID(N'dbo.[Order]')
          AND [name] = N'OrderStatus'
          AND [system_type_id] = TYPE_ID(N'nvarchar')
          AND [max_length] = 40 AND [is_nullable] = 0 AND [is_computed] = 0
    )
        THROW 50013, N'OrderStatus phải là NVARCHAR(20) NOT NULL. Hãy đối chiếu cấu trúc bảng Order trước khi tiếp tục.', 1;

    EXEC(N'IF EXISTS (
        SELECT 1 FROM [dbo].[Order]
        WHERE [PaymentStatus] NOT IN (N''Unpaid'', N''Paid'', N''Refunded'')
           OR ([PaymentStatus] = N''Refunded'' AND [OrderStatus] <> N''Cancelled'')
    )
        THROW 50014, N''Có trạng thái thanh toán không hợp lệ hoặc đơn đã hoàn tiền chưa được hủy. Hãy kiểm tra các đơn này trước khi tiếp tục.'', 1;');

    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'dbo.[Order]')
          AND [name] = N'CK_Order_Payment'
    )
        EXEC(N'ALTER TABLE [dbo].[Order] DROP CONSTRAINT [CK_Order_Payment];');

    EXEC(N'ALTER TABLE [dbo].[Order] WITH CHECK
        ADD CONSTRAINT [CK_Order_Payment]
        CHECK ([PaymentStatus] IN (N''Unpaid'', N''Paid'', N''Refunded''));');

    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'dbo.[Order]')
          AND [name] = N'CK_Order_RefundState'
    )
        EXEC(N'ALTER TABLE [dbo].[Order] DROP CONSTRAINT [CK_Order_RefundState];');

    EXEC(N'ALTER TABLE [dbo].[Order] WITH CHECK
        ADD CONSTRAINT [CK_Order_RefundState]
        CHECK ([PaymentStatus] <> N''Refunded'' OR [OrderStatus] = N''Cancelled'');');

    COMMIT TRANSACTION;
    PRINT N'Đã bổ sung trạng thái Refunded (Đã hoàn tiền) cho các đơn đã hủy.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

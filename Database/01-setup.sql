
USE [master];
GO
IF DB_ID(N'TechPartsStore') IS NULL
    EXEC(N'CREATE DATABASE [TechPartsStore]');
GO
USE [TechPartsStore];
GO
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[User] (
        [Username] NVARCHAR(100) NOT NULL,
        [Password] NVARCHAR(255) NOT NULL,
        [UserRole] NVARCHAR(1) NOT NULL,
        CONSTRAINT [PK_User] PRIMARY KEY ([Username]),
        CONSTRAINT [CK_User_Role] CHECK ([UserRole] IN (N'A', N'C'))
    );
END;
GO

IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Customer] (
        [CustomerID] INT IDENTITY(1,1) NOT NULL,
        [CustomerName] NVARCHAR(120) NOT NULL,
        [CustomerPhone] NVARCHAR(15) NOT NULL,
        [CustomerEmail] NVARCHAR(255) NULL,
        [CustomerAddress] NVARCHAR(500) NULL,
        [Username] NVARCHAR(100) NOT NULL,
        CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerID]),
        CONSTRAINT [FK_User_Customer] FOREIGN KEY ([Username])
            REFERENCES [dbo].[User] ([Username])
    );
    CREATE UNIQUE INDEX [UX_Customer_Username] ON [dbo].[Customer] ([Username]);
END;
GO

IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Category] (
        [CategoryID] INT IDENTITY(1,1) NOT NULL,
        [CategoryName] NVARCHAR(100) NOT NULL,
        CONSTRAINT [PK_Category] PRIMARY KEY ([CategoryID])
    );
END;
GO

IF OBJECT_ID(N'dbo.Product', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Product] (
        [ProductID] INT IDENTITY(1,1) NOT NULL,
        [CategoryID] INT NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,

        [ProductDecription] NVARCHAR(MAX) NOT NULL,
        [ProductPrice] DECIMAL(18,2) NOT NULL,
        [ProductImage] NVARCHAR(255) NULL,
        [Brand] NVARCHAR(100) NOT NULL,
        [DeviceType] NVARCHAR(10) NOT NULL,
        [StockQuantity] INT NOT NULL CONSTRAINT [DF_Product_Stock] DEFAULT (0),
        [Specifications] NVARCHAR(MAX) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Product_Active] DEFAULT (1),
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_Product] PRIMARY KEY ([ProductID]),
        CONSTRAINT [FK_Pro_Category] FOREIGN KEY ([CategoryID])
            REFERENCES [dbo].[Category] ([CategoryID]),
        CONSTRAINT [CK_Product_Price] CHECK ([ProductPrice] > 0),
        CONSTRAINT [CK_Product_Stock] CHECK ([StockQuantity] >= 0),
        CONSTRAINT [CK_Product_DeviceType] CHECK ([DeviceType] IN (N'PC', N'Laptop', N'Both'))
    );
    CREATE INDEX [IX_Product_CategoryID] ON [dbo].[Product] ([CategoryID]);
END;
GO

IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Order] (
        [OrderID] INT IDENTITY(1,1) NOT NULL,
        [CustomerID] INT NOT NULL,

        [OrderDate] DATETIME2 NOT NULL CONSTRAINT [DF_Order_Date] DEFAULT (SYSUTCDATETIME()),
        [TotalAmount] DECIMAL(18,2) NOT NULL,
        [PaymentStatus] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Order_Payment] DEFAULT (N'Unpaid'),
        [AddressDelivery] NVARCHAR(500) NOT NULL,
        [OrderStatus] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Order_Status] DEFAULT (N'Pending'),
        CONSTRAINT [PK_Order] PRIMARY KEY ([OrderID]),
        CONSTRAINT [FK_Order_Customer] FOREIGN KEY ([CustomerID])
            REFERENCES [dbo].[Customer] ([CustomerID]),
        CONSTRAINT [CK_Order_Total] CHECK ([TotalAmount] > 0),
        CONSTRAINT [CK_Order_Payment] CHECK ([PaymentStatus] IN (N'Unpaid', N'Paid')),
        CONSTRAINT [CK_Order_Status] CHECK ([OrderStatus] IN
            (N'Pending', N'Processing', N'Shipping', N'Delivered', N'Cancelled'))
    );
    CREATE INDEX [IX_Order_CustomerID] ON [dbo].[Order] ([CustomerID]);
END;
GO

IF OBJECT_ID(N'dbo.OrderDetail', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderDetail] (
        [ID] INT IDENTITY(1,1) NOT NULL,
        [ProductID] INT NOT NULL,
        [OrderID] INT NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [TotalPrice] AS ([Quantity] * [UnitPrice]) PERSISTED,
        CONSTRAINT [PK_OrderDetail] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_OrderDetail_Product] FOREIGN KEY ([ProductID])
            REFERENCES [dbo].[Product] ([ProductID]),
        CONSTRAINT [FK_OrderDetail_Order] FOREIGN KEY ([OrderID])
            REFERENCES [dbo].[Order] ([OrderID]),
        CONSTRAINT [CK_OrderDetail_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_OrderDetail_UnitPrice] CHECK ([UnitPrice] > 0)
    );
    CREATE INDEX [IX_OrderDetail_OrderID] ON [dbo].[OrderDetail] ([OrderID]);
    CREATE INDEX [IX_OrderDetail_ProductID] ON [dbo].[OrderDetail] ([ProductID]);
END;
GO


IF COL_LENGTH(N'dbo.Product', N'DeviceType') IS NULL
    OR COL_LENGTH(N'dbo.Product', N'StockQuantity') IS NULL
    OR COL_LENGTH(N'dbo.Product', N'RowVersion') IS NULL
    OR COL_LENGTH(N'dbo.[Order]', N'OrderStatus') IS NULL
    OR COL_LENGTH(N'dbo.[User]', N'Password') <> 510
    THROW 50001, N'CSDL không đúng cấu trúc TechPartsStore. Hãy tạo CSDL học tập mới hoặc đối chiếu schema trước khi tiếp tục.', 1;
GO
PRINT N'Đã chuẩn bị cấu trúc TechPartsStore. Tiếp theo chạy 02-seed.sql.';
GO

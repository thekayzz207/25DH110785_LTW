USE [TechPartsStore];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [Username] = N'admin')
    INSERT INTO [dbo].[User] ([Username], [Password], [UserRole]) VALUES
    (N'admin', N'PBKDF2$100000$Dcc7D7FL5t0IT88OTNx8rQ==$VamyDeIA6tWJ+03crRKmE+HBVW7iF0Nzoz3wgyhp3BE=', N'A');

IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [Username] = N'demo')
    INSERT INTO [dbo].[User] ([Username], [Password], [UserRole]) VALUES
    (N'demo', N'PBKDF2$100000$0eiV/W025Imfn/Z6xeZVaA==$/voAt+hxkmj3nrfZQcEVk6VHhVe7fB/RGepSblDNk2k=', N'C');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Customer] WHERE [Username] = N'demo')
    AND EXISTS (SELECT 1 FROM [dbo].[User] WHERE [Username] = N'demo' AND [UserRole] = N'C')
    INSERT INTO [dbo].[Customer]
        ([CustomerName], [CustomerPhone], [CustomerEmail], [CustomerAddress], [Username])
    VALUES (N'Khách hàng Demo', N'0900000000', N'demo@example.test', N'123 Nguyễn Văn Linh, TP. Hồ Chí Minh', N'demo');

DECLARE @Categories TABLE ([Name] NVARCHAR(100));
INSERT INTO @Categories VALUES
    (N'CPU'), (N'Card đồ họa'), (N'RAM'), (N'SSD'),
    (N'Mainboard'), (N'Tản nhiệt'), (N'Nguồn'), (N'Vỏ case');
INSERT INTO [dbo].[Category] ([CategoryName])
SELECT c.[Name] FROM @Categories c
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Category] e WHERE e.[CategoryName] = c.[Name]);

DECLARE @Products TABLE (
    [CategoryName] NVARCHAR(100), [ProductName] NVARCHAR(255),
    [ProductDecription] NVARCHAR(MAX), [ProductPrice] DECIMAL(18,2),
    [ImageKey] NVARCHAR(30), [Brand] NVARCHAR(100), [DeviceType] NVARCHAR(10),
    [StockQuantity] INT, [Specifications] NVARCHAR(MAX)
);
INSERT INTO @Products VALUES
    (N'CPU', N'Intel Core i5-14400F', N'CPU cho PC gaming và làm việc; bản F cần card đồ họa rời.', 4390000, N'cpu', N'Intel', N'PC', 24,
     N'Socket: LGA1700 | Nhân/luồng: 10 (6P+4E) / 16 | Xung turbo tối đa: 4.7 GHz | Cache L3: 20 MB | Công suất cơ bản: 65 W | Không có iGPU'),
    (N'CPU', N'AMD Ryzen 5 7600', N'Nền tảng AM5 cho máy tính chơi game và nâng cấp dài hạn.', 4790000, N'cpu', N'AMD', N'PC', 18,
     N'Socket: AM5 | Nhân/luồng: 6 / 12 | Xung turbo tối đa: 5.1 GHz | Cache L3: 32 MB | TDP: 65 W | Bộ nhớ: DDR5'),
    (N'Card đồ họa', N'ASUS Dual GeForce RTX 4060 OC 8GB', N'Card đồ họa rời cho PC, phù hợp chơi game Full HD.', 8190000, N'gpu', N'ASUS', N'PC', 12,
     N'GPU: NVIDIA GeForce RTX 4060 | VRAM: 8 GB GDDR6 | Bus bộ nhớ: 128-bit | Giao tiếp: PCIe 4.0 x8 | Nguồn phụ: 1 x 8-pin | DLSS 3'),
    (N'Card đồ họa', N'Sapphire Pulse Radeon RX 7600 8GB', N'GPU AMD cho PC gaming 1080p với thiết kế hai quạt.', 6990000, N'gpu', N'Sapphire', N'PC', 10,
     N'GPU: AMD Radeon RX 7600 | VRAM: 8 GB GDDR6 | Bus bộ nhớ: 128-bit | Giao tiếp: PCIe 4.0 x8 | Nguồn phụ: 1 x 8-pin'),
    (N'RAM', N'Kingston FURY Beast 16GB DDR4-3200 Desktop', N'RAM DIMM cho máy bàn hỗ trợ DDR4.', 890000, N'ram', N'Kingston', N'PC', 45,
     N'Dung lượng: 1 x 16 GB | Chuẩn: DDR4 | Tốc độ: 3200 MT/s | Định dạng: UDIMM 288-pin | Điện áp XMP: 1.35 V | Độ trễ: CL16'),
    (N'RAM', N'Corsair Vengeance 32GB DDR5-6000 Desktop', N'Bộ kit hai thanh dành cho PC hỗ trợ DDR5 và profile XMP.', 2990000, N'ram', N'Corsair', N'PC', 20,
     N'Dung lượng: 2 x 16 GB | Chuẩn: DDR5 | Tốc độ: 6000 MT/s | Định dạng: UDIMM 288-pin | Độ trễ: CL36 | Điện áp XMP: 1.35 V'),
    (N'RAM', N'Crucial 16GB DDR4-3200 SODIMM Laptop', N'RAM nâng cấp laptop có khe DDR4 SODIMM rời; không dùng cho khe DDR5.', 790000, N'ram', N'Crucial', N'Laptop', 38,
     N'Dung lượng: 1 x 16 GB | Chuẩn: DDR4 | Tốc độ: 3200 MT/s | Định dạng: SODIMM 260-pin | Điện áp: 1.2 V | Độ trễ: CL22'),
    (N'RAM', N'Crucial 16GB DDR5-5600 SODIMM Laptop', N'RAM cho laptop có khe DDR5 SODIMM; kiểm tra giới hạn CPU và BIOS.', 1290000, N'ram', N'Crucial', N'Laptop', 28,
     N'Dung lượng: 1 x 16 GB | Chuẩn: DDR5 | Tốc độ: 5600 MT/s | Định dạng: SODIMM 262-pin | Điện áp: 1.1 V | Độ trễ: CL46'),
    (N'SSD', N'Samsung 990 EVO 1TB NVMe M.2', N'SSD cho PC/laptop có khe M.2 NVMe 2280 tương thích.', 2190000, N'ssd', N'Samsung', N'Both', 32,
     N'Dung lượng: 1 TB | Kích thước: M.2 2280 | Giao tiếp: PCIe 4.0 x4 / 5.0 x2 NVMe | Đọc tuần tự tối đa: 5000 MB/s | Ghi tuần tự tối đa: 4200 MB/s'),
    (N'SSD', N'WD Blue SN580 1TB NVMe M.2', N'Ổ SSD NVMe cho nâng cấp dung lượng và tốc độ khởi động.', 1690000, N'ssd', N'Western Digital', N'Both', 40,
     N'Dung lượng: 1 TB | Kích thước: M.2 2280 | Giao tiếp: PCIe 4.0 x4 NVMe | Đọc tuần tự tối đa: 4150 MB/s | Ghi tuần tự tối đa: 4150 MB/s'),
    (N'SSD', N'Crucial MX500 500GB SATA 2.5 inch', N'SSD SATA cho PC/laptop có khoang 2.5 inch và cổng SATA phù hợp.', 1090000, N'ssd', N'Crucial', N'Both', 25,
     N'Dung lượng: 500 GB | Kích thước: 2.5 inch, dày 7 mm | Giao tiếp: SATA 6 Gb/s | Đọc tuần tự tối đa: 560 MB/s | Ghi tuần tự tối đa: 510 MB/s'),
    (N'SSD', N'Kingston NV2 500GB NVMe M.2', N'SSD M.2 phổ thông cho máy có khe PCIe NVMe 2280.', 890000, N'ssd', N'Kingston', N'Both', 35,
     N'Dung lượng: 500 GB | Kích thước: M.2 2280 | Giao tiếp: PCIe 4.0 x4 NVMe | Đọc tuần tự tối đa: 3500 MB/s | Ghi tuần tự tối đa: 2100 MB/s'),
    (N'Mainboard', N'MSI PRO B760M-A WIFI DDR4', N'Mainboard Intel cho case mATX/ATX; phiên bản này chỉ dùng RAM DDR4.', 3290000, N'mainboard', N'MSI', N'PC', 16,
     N'Chipset: Intel B760 | Socket: LGA1700 | Kích thước: Micro-ATX | RAM: 4 khe DDR4 | Lưu trữ: 2 khe M.2 PCIe 4.0 | Kết nối: Wi-Fi 6E, LAN 2.5G | CPU đời mới có thể cần cập nhật BIOS'),
    (N'Mainboard', N'ASUS TUF GAMING B650M-PLUS WIFI', N'Mainboard AM5 cho Ryzen; cần RAM DDR5 và case hỗ trợ Micro-ATX.', 4990000, N'mainboard', N'ASUS', N'PC', 9,
     N'Chipset: AMD B650 | Socket: AM5 | Kích thước: Micro-ATX | RAM: 4 khe DDR5 | Lưu trữ: 2 khe M.2 | Kết nối: Wi-Fi 6, LAN 2.5G'),
    (N'Tản nhiệt', N'DeepCool AG400', N'Tản nhiệt khí dạng tháp cho CPU desktop; kiểm tra chiều cao tối đa của case.', 590000, N'cooling', N'DeepCool', N'PC', 30,
     N'Chiều cao: 150 mm | Quạt: 120 mm PWM | Ống dẫn nhiệt: 4 | Socket: Intel LGA1700/1200/115x, AMD AM5/AM4 | Không dùng làm tản nhiệt laptop'),
    (N'Tản nhiệt', N'Cooler Master Hyper 212 Black', N'Tản nhiệt khí cho PC với quạt PWM; kiểm tra bộ ngàm theo phiên bản hộp.', 890000, N'cooling', N'Cooler Master', N'PC', 18,
     N'Quạt: 120 mm PWM | Thiết kế: tháp đơn, 4 ống dẫn nhiệt | Chiều cao: khoảng 152 mm tùy phiên bản | Phiên bản hỗ trợ LGA1700 và AM5/AM4'),
    (N'Nguồn', N'Corsair CX650 650W 80 PLUS Bronze', N'Nguồn ATX cho PC; kiểm tra công suất yêu cầu và đầu cắm card đồ họa.', 1390000, N'psu', N'Corsair', N'PC', 21,
     N'Công suất: 650 W | Chuẩn: ATX | Hiệu suất: 80 PLUS Bronze | Dây nguồn: cố định | Quạt: 120 mm | Không phải adapter sạc laptop'),
    (N'Nguồn', N'MSI MAG A750GL PCIE5 750W', N'Nguồn modular cho PC có GPU hiện đại.', 2490000, N'psu', N'MSI', N'PC', 13,
     N'Công suất: 750 W | Hiệu suất: 80 PLUS Gold | Chuẩn: ATX 3.0 | Dây nguồn: full modular | Có đầu cắm GPU 16-pin theo bộ dây của nhà sản xuất'),
    (N'Vỏ case', N'Cooler Master MasterBox Q300L', N'Case gọn cho cấu hình Micro-ATX hoặc Mini-ITX.', 1190000, N'case', N'Cooler Master', N'PC', 14,
     N'Mainboard: Micro-ATX / Mini-ITX | GPU tối đa: 360 mm | Tản CPU tối đa: 159 mm | Nguồn: ATX | Mặt hông: acrylic'),
    (N'Vỏ case', N'Montech AIR 100 ARGB', N'Case Micro-ATX thông thoáng với bốn quạt ARGB đi kèm.', 1490000, N'case', N'Montech', N'PC', 17,
     N'Mainboard: Micro-ATX / Mini-ITX | GPU tối đa: 330 mm | Tản CPU tối đa: 161 mm | Nguồn: ATX tối đa 160 mm | Quạt kèm: 4 x 120 mm ARGB');

INSERT INTO [dbo].[Product]
    ([CategoryID], [ProductName], [ProductDecription], [ProductPrice], [ProductImage],
     [Brand], [DeviceType], [StockQuantity], [Specifications], [IsActive])
SELECT
    (SELECT MIN(c.[CategoryID]) FROM [dbo].[Category] c WHERE c.[CategoryName] = p.[CategoryName]),
    p.[ProductName], p.[ProductDecription], p.[ProductPrice],
    N'~/Content/images/' + p.[ImageKey] + N'.svg',
    p.[Brand], p.[DeviceType], p.[StockQuantity], p.[Specifications], 1
FROM @Products p
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Product] e WHERE e.[ProductName] = p.[ProductName]);

COMMIT TRANSACTION;
PRINT N'Đã bổ sung dữ liệu mẫu. admin / Admin@12345; demo / Demo@12345.';
GO

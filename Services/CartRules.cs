using System;
using System.Collections.Generic;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Services
{
    public static class CartRules
    {
        public static bool TryBuildReorder(Dictionary<int, int> current, IEnumerable<OrderDetail> lines,
            out Dictionary<int, int> updated, out string message)
        {
            updated = null;
            message = "Giỏ hàng hiện tại không hợp lệ. Vui lòng kiểm tra giỏ hàng trước khi mua lại.";
            if (current == null || current.Count > 50) return false;
            foreach (var entry in current)
            {
                if (entry.Key <= 0 || entry.Value < 1 || entry.Value > 99) return false;
            }

            message = "Đơn hàng không có sản phẩm hợp lệ để mua lại.";
            if (lines == null) return false;
            var staged = new Dictionary<int, int>(current);
            bool hasLine = false;
            foreach (var line in lines)
            {
                hasLine = true;
                var name = line != null && line.Product != null && !string.IsNullOrWhiteSpace(line.Product.ProductName)
                    ? line.Product.ProductName : line != null ? "sản phẩm #" + line.ProductID : "sản phẩm trong đơn";
                if (line == null || line.ProductID <= 0 || line.Product == null || line.Product.ProductID != line.ProductID)
                {
                    message = "Không thể mua lại " + name + ": dữ liệu sản phẩm không còn hợp lệ.";
                    return false;
                }
                string itemMessage;
                if (!TryAdd(staged, line.Product, line.Quantity, out itemMessage))
                {
                    message = "Không thể mua lại " + name + ": " + itemMessage;
                    return false;
                }
            }
            if (!hasLine) return false;

            // Only expose the copy after every stored line has passed current stock and cart limits.
            updated = staged;
            message = "Đã thêm toàn bộ sản phẩm của đơn hàng vào giỏ hàng theo số lượng đã mua.";
            return true;
        }

        public static bool TryNormalizeReorderKey(string raw, out string key)
        {
            key = null;
            Guid parsed;
            if (raw == null || raw.Length != 32 || !Guid.TryParseExact(raw, "N", out parsed) || parsed == Guid.Empty)
                return false;
            key = parsed.ToString("N");
            return true;
        }

        public static bool TryUpdate(Dictionary<int, int> cart, int productId, Product product, int quantity, out string message)
        {
            if (cart == null) throw new ArgumentNullException("cart");
            message = "Sản phẩm không còn trong giỏ hàng. Vui lòng kiểm tra lại.";
            if (!cart.ContainsKey(productId)) return false;
            if (quantity == 0)
            {
                cart.Remove(productId);
                message = "Đã xóa sản phẩm khỏi giỏ hàng.";
                return true;
            }
            message = "Số lượng không hợp lệ hoặc vượt tồn kho (tối đa 99 mỗi loại).";
            if (product == null || product.ProductID != productId || !product.IsActive || quantity < 1 || quantity > 99 || quantity > product.StockQuantity)
                return false;
            cart[productId] = quantity;
            message = "Đã cập nhật số lượng.";
            return true;
        }

        public static bool TryAdd(Dictionary<int, int> cart, Product product, int quantity, out string message)
        {
            if (cart == null) throw new ArgumentNullException("cart");
            message = "Số lượng không hợp lệ hoặc sản phẩm không đủ tồn kho (tối đa 99 mỗi loại).";
            if (product == null || !product.IsActive || quantity < 1 || quantity > 99) return false;

            int existing;
            bool alreadyInCart = cart.TryGetValue(product.ProductID, out existing);
            if (existing < 0 || existing > 99 || existing > 99 - quantity) return false;
            int desired = existing + quantity;
            if (desired > product.StockQuantity) return false;
            if (!alreadyInCart && cart.Count >= 50)
            {
                message = "Giỏ hàng chứa tối đa 50 loại sản phẩm.";
                return false;
            }

            cart[product.ProductID] = desired;
            message = "Đã thêm " + product.ProductName + " vào giỏ hàng.";
            return true;
        }
    }
}

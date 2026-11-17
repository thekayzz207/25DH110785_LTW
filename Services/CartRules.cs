using System;
using System.Collections.Generic;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Services
{
    public static class CartRules
    {
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

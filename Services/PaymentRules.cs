using System;
using System.IO;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Services
{
    public static class PaymentRules
    {
        public const string MethodPattern = "^(COD|BankTransfer)$";

        public static bool IsSupportedMethod(string method)
        {
            return method == "COD" || method == "BankTransfer";
        }

        public static bool IsValidLocalQrPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("~/Content/", StringComparison.Ordinal)
                || path.Contains("..") || path.Contains("\\") || path.Contains("?") || path.Contains("#")) return false;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp";
        }

        public static bool CanChangeStatus(Order order, string nextStatus)
        {
            if (order == null || !IsSupportedMethod(order.PaymentMethod) || order.PaymentStatus == "Refunded") return false;
            bool allowed = (order.OrderStatus == "Pending" && (nextStatus == "Processing" || nextStatus == "Cancelled"))
                || (order.OrderStatus == "Processing" && (nextStatus == "Shipping" || nextStatus == "Cancelled"))
                || (order.OrderStatus == "Shipping" && nextStatus == "Delivered");
            return allowed && (order.PaymentMethod != "BankTransfer" || order.PaymentStatus == "Paid" || nextStatus == "Cancelled");
        }

        public static string PaymentStatusAfterTransition(Order order, string nextStatus)
        {
            if (order == null) throw new ArgumentNullException("order");
            return order.PaymentMethod == "COD" && nextStatus == "Delivered" ? "Paid" : order.PaymentStatus;
        }

        public static bool CanConfirmBankReceipt(Order order)
        {
            return order != null && order.PaymentMethod == "BankTransfer" && order.OrderStatus != "Cancelled"
                && (order.PaymentStatus == "Unpaid" || order.PaymentStatus == "Paid");
        }

        public static bool CanCustomerCancel(Order order)
        {
            return order != null && order.OrderStatus == "Pending" && IsSupportedMethod(order.PaymentMethod)
                && (order.PaymentStatus == "Unpaid" || order.PaymentStatus == "Paid");
        }

        public static bool CanConfirmRefund(Order order)
        {
            return order != null && order.OrderStatus == "Cancelled" && order.PaymentStatus == "Paid"
                && IsSupportedMethod(order.PaymentMethod);
        }

        public static string TransferReference(int orderId)
        {
            if (orderId <= 0) throw new ArgumentOutOfRangeException("orderId");
            return "AKS" + orderId.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

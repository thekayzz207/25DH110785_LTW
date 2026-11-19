using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Services;
using LTW_25DH110785.Models;
using LTW_25DH110785.Areas.Admin.Models;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class OrdersController : AdminController
    {
        public ActionResult Index(int? customerId, string q, string sort = "newest", int page = 1,
            string paymentStatus = "all", string orderDate = null)
        {
            q = AdminOrderQuery.NormalizeSearch(q);
            sort = AdminOrderQuery.NormalizeSort(sort);
            string normalizedPayment;
            var paymentValid = AdminOrderQuery.TryNormalizePaymentStatus(paymentStatus, out normalizedPayment);
            if (!paymentValid) ModelState.AddModelError("paymentStatus", "Trạng thái thanh toán không hợp lệ. Vui lòng chọn Tất cả, Đã thanh toán, Chưa thanh toán hoặc Đã hoàn tiền.");
            orderDate = (orderDate ?? "").Trim();
            DateTime? startUtc, endUtc;
            var dateValid = AdminOrderQuery.TryDateRange(orderDate, out startUtc, out endUtc);
            if (!dateValid) ModelState.AddModelError("orderDate", "Ngày đặt hàng không hợp lệ. Vui lòng nhập ngày theo định dạng yyyy-MM-dd.");

            // Invalid filters fail closed; a malformed date must not expose an unfiltered list.
            var query = AdminOrderQuery.Filter(db.Orders.AsNoTracking(), customerId, q,
                normalizedPayment, startUtc, endUtc, paymentValid && dateValid && ModelState.IsValid);
            var count = query.Count();
            var totalPages = AdminOrderQuery.TotalPages(count);
            page = AdminOrderQuery.NormalizePage(page, totalPages);
            var items = AdminOrderQuery.PageSlice(AdminOrderQuery.Sort(query, sort), page)
                .Include(order => order.Customer).ToList();
            var model = new AdminOrderListVM
            {
                Items = items,
                Page = page,
                TotalPages = totalPages,
                TotalItems = count,
                Q = q,
                Sort = sort,
                CustomerId = customerId,
                PaymentStatus = normalizedPayment,
                OrderDate = orderDate
            };
            ViewBag.CustomerId = customerId;
            return View(model);
        }
        public ActionResult Details(int id)
        {
            var order = db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.OrderDetails.Select(d => d.Product)).SingleOrDefault(o => o.OrderID == id);
            if (order == null) return HttpNotFound();
            return View("DetailedOrder", order);
        }
        [HttpGet]
        public ActionResult UpdateStatus(int id)
        {
            var order = db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.OrderDetails.Select(d => d.Product)).SingleOrDefault(o => o.OrderID == id);
            if (order == null) return HttpNotFound();
            return View("UpdateStatus", order);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    // Lock before checking the state so two cancellation requests cannot both restock.
                    var order = db.Orders.SqlQuery("SELECT * FROM [dbo].[Order] WITH (UPDLOCK,HOLDLOCK) WHERE [OrderID] = @p0", id).SingleOrDefault();
                    if (order == null) return HttpNotFound();
                    bool allowed = PaymentRules.CanChangeStatus(order, status);
                    if (!allowed)
                    {
                        TempData["Error"] = order.PaymentMethod == "BankTransfer" && order.PaymentStatus != "Paid" && status != "Cancelled"
                            ? "Đơn chuyển khoản cần được quản trị viên xác nhận đã thực nhận đủ tiền trước khi xử lý, giao hàng hoặc hoàn tất."
                            : "Không thể chuyển sang trạng thái đã chọn. Đơn có thể vừa được người khác cập nhật.";
                        return RedirectToAction("Details", new { id });
                    }
                    if (status == "Cancelled")
                    {
                        var details = db.OrderDetails.Where(d => d.OrderID == id).OrderBy(d => d.ProductID).ToList();
                        foreach (var line in details)
                        {
                            db.Database.ExecuteSqlCommand("UPDATE [dbo].[Product] SET [StockQuantity] = [StockQuantity] + @p0 WHERE [ProductID] = @p1", line.Quantity, line.ProductID);
                        }
                    }
                    order.OrderStatus = status;
                    // A status change must never erase a bank payment already verified by an admin.
                    // COD is collected at delivery; a QR scan does not verify a bank receipt.
                    order.PaymentStatus = PaymentRules.PaymentStatusAfterTransition(order, status);
                    db.SaveChanges();
                    transaction.Commit();
                    TempData["Success"] = status == "Cancelled" && order.PaymentStatus == "Paid"
                        ? "Đã hủy đơn và hoàn lại tồn kho. Đơn đã thanh toán: cần đối chiếu và xử lý hoàn tiền thủ công; hệ thống không tự hoàn tiền ngân hàng."
                        : "Đã cập nhật trạng thái đơn hàng.";
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Chưa cập nhật được đơn hàng. Hãy tải lại và thử lại.";
            }
            catch (System.Data.SqlClient.SqlException)
            {
                TempData["Error"] = "Đơn hàng đang được xử lý ở phiên khác. Hãy tải lại trước khi cập nhật.";
            }
            return RedirectToAction("Details", new { id });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmPayment(int id)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    // The order lock makes confirmation and cancellation mutually consistent.
                    var order = db.Orders.SqlQuery("SELECT * FROM [dbo].[Order] WITH (UPDLOCK,HOLDLOCK) WHERE [OrderID] = @p0", id).SingleOrDefault();
                    if (order == null) return HttpNotFound();
                    if (order.PaymentMethod != "BankTransfer")
                    {
                        TempData["Error"] = "Chỉ xác nhận nhận tiền ngân hàng cho đơn thanh toán bằng chuyển khoản. Đơn COD được ghi nhận thanh toán khi đã giao và thu tiền.";
                        return RedirectToAction("Details", new { id });
                    }
                    if (order.OrderStatus == "Cancelled")
                    {
                        TempData["Error"] = "Đơn đã hủy nên không thể xác nhận thanh toán. Nếu có tiền về, hãy đối chiếu và xử lý hoàn tiền thủ công.";
                        return RedirectToAction("Details", new { id });
                    }
                    if (!PaymentRules.CanConfirmBankReceipt(order))
                    {
                        TempData["Error"] = "Trạng thái thanh toán không hợp lệ. Vui lòng kiểm tra lại đơn hàng.";
                        return RedirectToAction("Details", new { id });
                    }
                    if (order.PaymentStatus == "Paid")
                    {
                        transaction.Commit();
                        TempData["Success"] = "Đơn hàng đã được xác nhận thanh toán trước đó; không có thay đổi thêm.";
                        return RedirectToAction("Details", new { id });
                    }
                    // This admin-only POST represents an explicit check of the actual bank receipt.
                    // It does not call a bank API and does not change fulfillment status.
                    order.PaymentStatus = "Paid";
                    db.SaveChanges();
                    transaction.Commit();
                    TempData["Success"] = "Đã ghi nhận xác nhận của quản trị viên: ngân hàng đã nhận đủ tiền cho đơn hàng. Có thể tiếp tục xử lý đơn.";
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Chưa lưu được xác nhận thanh toán. Hãy tải lại đơn hàng và kiểm tra trước khi thử lại.";
            }
            catch (System.Data.SqlClient.SqlException)
            {
                TempData["Error"] = "Đơn hàng đang được cập nhật ở phiên khác. Hãy tải lại trước khi xác nhận nhận tiền.";
            }
            return RedirectToAction("Details", new { id });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmRefund(int id)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var order = db.Orders.SqlQuery(
                        "SELECT * FROM [dbo].[Order] WITH (UPDLOCK,HOLDLOCK) WHERE [OrderID] = @p0",
                        id).SingleOrDefault();
                    if (order == null) return HttpNotFound();

                    if (order.OrderStatus == "Cancelled" && order.PaymentStatus == "Refunded")
                    {
                        transaction.Commit();
                        TempData["Success"] = "Đơn hàng đã được ghi nhận hoàn tiền trước đó.";
                        return RedirectToAction("Details", new { id });
                    }
                    if (!PaymentRules.CanConfirmRefund(order))
                    {
                        TempData["Error"] = "Chỉ có thể ghi nhận đã hoàn tiền cho đơn đã hủy và đã thanh toán. Vui lòng tải lại để kiểm tra trạng thái.";
                        return RedirectToAction("Details", new { id });
                    }

                    order.PaymentStatus = "Refunded";
                    db.SaveChanges();
                    transaction.Commit();
                    TempData["Success"] = "Đã ghi nhận hoàn đủ tiền cho đơn hàng.";
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Chưa lưu được trạng thái hoàn tiền. Vui lòng tải lại trang và kiểm tra cấu trúc database hỗ trợ trạng thái Đã hoàn tiền.";
            }
            catch (System.Data.SqlClient.SqlException)
            {
                TempData["Error"] = "Đơn hàng đang được cập nhật. Vui lòng tải lại để kiểm tra trước khi xác nhận hoàn tiền.";
            }
            return RedirectToAction("Details", new { id });
        }
    }

    // Pure query helpers keep filters and paging server-side and can be tested without MVC/EF.
    public static class AdminOrderQuery
    {
        public const int PageSize = 15;
        public const int SearchLimit = 200;

        public static string NormalizeSearch(string q)
        {
            q = string.Join(" ", (q ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return q.Length <= SearchLimit ? q : q.Substring(0, SearchLimit).TrimEnd();
        }

        public static string NormalizeSort(string sort)
        {
            switch ((sort ?? "").Trim().ToLowerInvariant())
            {
                case "oldest": return "oldest";
                case "total_desc": return "total_desc";
                case "total_asc": return "total_asc";
                default: return "newest";
            }
        }

        public static bool TryNormalizePaymentStatus(string value, out string normalized)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0 || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            { normalized = "all"; return true; }
            if (string.Equals(value, "Paid", StringComparison.OrdinalIgnoreCase))
            { normalized = "Paid"; return true; }
            if (string.Equals(value, "Unpaid", StringComparison.OrdinalIgnoreCase))
            { normalized = "Unpaid"; return true; }
            if (string.Equals(value, "Refunded", StringComparison.OrdinalIgnoreCase))
            { normalized = "Refunded"; return true; }
            normalized = value;
            return false;
        }

        public static bool TryDateRange(string value, out DateTime? startUtc, out DateTime? endUtc)
        {
            startUtc = null;
            endUtc = null;
            value = (value ?? "").Trim();
            if (value.Length == 0) return true;
            DateTime localDate;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out localDate)) return false;
            try
            {
                startUtc = DateTime.SpecifyKind(localDate.AddHours(-7), DateTimeKind.Utc);
                endUtc = startUtc.Value.AddDays(1);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                startUtc = null;
                endUtc = null;
                return false;
            }
        }

        public static int? ParseOrderId(string q)
        {
            var code = NormalizeSearch(q);
            if (code.StartsWith("#", StringComparison.Ordinal)) code = code.Substring(1);
            if (code.StartsWith("NX-", StringComparison.OrdinalIgnoreCase)) code = code.Substring(3);
            else if (code.StartsWith("AKS", StringComparison.OrdinalIgnoreCase)) code = code.Substring(3);
            int id;
            return int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0 ? (int?)id : null;
        }

        public static IQueryable<Order> Filter(IQueryable<Order> orders, int? customerId, string q,
            string paymentStatus, DateTime? startUtc, DateTime? endUtc, bool validFilters = true)
        {
            string normalizedPayment;
            if (!validFilters || !TryNormalizePaymentStatus(paymentStatus, out normalizedPayment)) return orders.Where(order => false);
            if (startUtc.HasValue != endUtc.HasValue || (startUtc.HasValue && startUtc.Value >= endUtc.Value)) return orders.Where(order => false);
            if (customerId.HasValue) orders = orders.Where(order => order.CustomerID == customerId.Value);
            q = NormalizeSearch(q);
            if (q.Length > 0)
            {
                var parsedId = ParseOrderId(q);
                if (parsedId.HasValue)
                {
                    var id = parsedId.Value;
                    orders = orders.Where(order => order.OrderID == id || order.Customer.CustomerName.Contains(q)
                        || order.Customer.Username.Contains(q) || order.Customer.CustomerPhone.Contains(q));
                }
                else orders = orders.Where(order => order.Customer.CustomerName.Contains(q)
                    || order.Customer.Username.Contains(q) || order.Customer.CustomerPhone.Contains(q));
            }
            if (normalizedPayment != "all") orders = orders.Where(order => order.PaymentStatus == normalizedPayment);
            if (startUtc.HasValue)
            {
                var start = startUtc.Value;
                var end = endUtc.Value;
                orders = orders.Where(order => order.OrderDate >= start && order.OrderDate < end);
            }
            return orders;
        }

        public static IOrderedQueryable<Order> Sort(IQueryable<Order> orders, string sort)
        {
            switch (NormalizeSort(sort))
            {
                case "oldest": return orders.OrderBy(order => order.OrderID);
                case "total_desc": return orders.OrderByDescending(order => order.TotalAmount).ThenByDescending(order => order.OrderID);
                case "total_asc": return orders.OrderBy(order => order.TotalAmount).ThenByDescending(order => order.OrderID);
                default: return orders.OrderByDescending(order => order.OrderID);
            }
        }

        public static int TotalPages(int count)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(0, count) / (double)PageSize));
        }

        public static int NormalizePage(int page, int totalPages)
        {
            return Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        }

        public static IQueryable<Order> PageSlice(IQueryable<Order> sortedOrders, int page)
        {
            var skip = (int)Math.Min(int.MaxValue, ((long)Math.Max(1, page) - 1) * PageSize);
            return sortedOrders.Skip(skip).Take(PageSize);
        }
    }
}

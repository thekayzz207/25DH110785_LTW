using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Services;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class OrdersController : AdminController
    {
        public ActionResult Index(int? customerId, string q, string sort = "newest", int page = 1)
        {
            q = (q ?? "").Trim();
            var query = db.Orders.AsNoTracking().Include(o => o.Customer).AsQueryable();
            if (customerId.HasValue) query = query.Where(o => o.CustomerID == customerId.Value);
            if (q.Length > 0)
            {
                int orderId;
                var code = q.TrimStart('#');
                if (code.StartsWith("NX-", StringComparison.OrdinalIgnoreCase)) code = code.Substring(3);
                else if (code.StartsWith("AKS", StringComparison.OrdinalIgnoreCase)) code = code.Substring(3);
                if (int.TryParse(code, out orderId))
                    query = query.Where(o => o.OrderID == orderId || o.Customer.CustomerPhone.Contains(q));
                else query = query.Where(o => o.Customer.CustomerName.Contains(q) || o.Customer.Username.Contains(q) || o.Customer.CustomerPhone.Contains(q));
            }
            switch (sort)
            {
                case "oldest": query = query.OrderBy(o => o.OrderID); break;
                case "total_desc": query = query.OrderByDescending(o => o.TotalAmount).ThenByDescending(o => o.OrderID); break;
                case "total_asc": query = query.OrderBy(o => o.TotalAmount).ThenByDescending(o => o.OrderID); break;
                default: sort = "newest"; query = query.OrderByDescending(o => o.OrderID); break;
            }
            ViewBag.CustomerId = customerId;
            return View(Paginate(query, page, q, sort));
        }
        public ActionResult Details(int id)
        {
            var order = db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.OrderDetails.Select(d => d.Product)).SingleOrDefault(o => o.OrderID == id);
            if (order == null) return HttpNotFound();
            return View(order);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
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
    }
}

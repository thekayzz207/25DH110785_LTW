using System;
using System.Collections.Generic;
using System.Globalization;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Models;
using LTW_25DH110785.Services;
using LTW_25DH110785.ViewModels;

namespace LTW_25DH110785.Controllers
{
    [Authorize(Roles="C")]
    public class OrderController : Controller
    {
        private readonly StoreDbContext db=new StoreDbContext();
        public ActionResult Checkout()
        {
            var cart=CartService.Read(db,Session); if(cart.Items.Count==0) return RedirectToAction("Index","Cart");
            var c=db.Customers.SingleOrDefault(x=>x.Username==User.Identity.Name); if(c==null) return HttpNotFound();
            // A session token handles repeated submits; the anti-forgery token handles CSRF.
            var token=Guid.NewGuid().ToString("N"); Session["CheckoutToken"]=token;
            var model = new CheckoutVM { CustomerName=c.CustomerName,CustomerPhone=c.CustomerPhone,AddressDelivery=c.CustomerAddress,Cart=cart,CheckoutToken=token,PaymentMethod="COD" };
            PopulatePayment(model);
            return View(model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Checkout(CheckoutVM model)
        {
            if (!string.IsNullOrEmpty(model.CheckoutToken) && model.CheckoutToken == (string)Session["CompletedCheckoutToken"] && Session["CompletedOrderId"] is int)
                return RedirectToAction("Success",new { id=(int)Session["CompletedOrderId"] });
            if(string.IsNullOrEmpty(model.CheckoutToken) || model.CheckoutToken!=(string)Session["CheckoutToken"])
            { TempData["Error"]="Phiên đặt hàng đã thay đổi. Vui lòng kiểm tra và xác nhận lại."; return RedirectToAction("Checkout"); }
            var c=db.Customers.SingleOrDefault(x=>x.Username==User.Identity.Name); if(c==null) return HttpNotFound();
            model.CustomerName=c.CustomerName; model.CustomerPhone=c.CustomerPhone;
            // Availability comes from the configured local QR, never posted form values.
            PopulatePayment(model);
            if (model.PaymentMethod == "BankTransfer" && !model.BankTransferAvailable)
                ModelState.AddModelError("PaymentMethod", "Chuyển khoản tạm chưa khả dụng. Vui lòng chọn thanh toán khi nhận hàng.");
            var cart=CartService.Get(Session);
            if(cart.Count==0) return RedirectToAction("Index","Cart");
            if(!ModelState.IsValid) { model.Cart=CartService.Read(db,Session); return View(model); }
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var order=new Order { CustomerID=c.CustomerID,OrderDate=DateTime.UtcNow,AddressDelivery=model.AddressDelivery.Trim(),PaymentMethod=model.PaymentMethod,PaymentStatus="Unpaid",OrderStatus="Pending" };
                    // Lock rows in a stable order. Re-read availability and prices inside the transaction.
                    foreach(var line in cart.OrderBy(x=>x.Key))
                    {
                        var p=db.Products.SqlQuery("SELECT * FROM dbo.Product WITH (UPDLOCK,HOLDLOCK) WHERE ProductID=@id",new SqlParameter("@id",line.Key)).SingleOrDefault();
                        if(p==null || !p.IsActive || line.Value<1 || line.Value>99 || p.StockQuantity<line.Value)
                            throw new InvalidOperationException("Một sản phẩm đã ngừng bán hoặc không đủ tồn kho. Vui lòng cập nhật giỏ hàng.");
                        order.OrderDetails.Add(new OrderDetail { ProductID=p.ProductID,Quantity=line.Value,UnitPrice=p.ProductPrice });
                        order.TotalAmount+=p.ProductPrice*line.Value; p.StockQuantity-=line.Value;
                    }
                    db.Orders.Add(order); db.SaveChanges(); tx.Commit();
                    cart.Clear(); Session["CompletedCheckoutToken"]=model.CheckoutToken; Session["CompletedOrderId"]=order.OrderID; Session.Remove("CheckoutToken");
                    return RedirectToAction("Success",new { id=order.OrderID });
                }
                catch(InvalidOperationException ex) { tx.Rollback(); TempData["Error"]=ex.Message; return RedirectToAction("Index","Cart"); }
                catch(System.Data.Entity.Infrastructure.DbUpdateException) { tx.Rollback(); TempData["Error"]="Chưa thể tạo đơn hàng. Vui lòng kiểm tra giỏ hàng và thử lại."; return RedirectToAction("Index","Cart"); }
                catch(SqlException) { tx.Rollback(); TempData["Error"]="Có giao dịch đồng thời. Vui lòng thử đặt hàng lại."; return RedirectToAction("Index","Cart"); }
            }
        }
        public ActionResult History(string q, string status = "all", string expanded = null)
        {
            q = OrderHistoryQuery.NormalizeSearch(q);
            status = OrderHistoryQuery.NormalizeStatus(status);
            expanded = OrderHistoryQuery.NormalizeExpanded(expanded);
            // Every count, search and slice starts from this ownership-scoped query.
            var owned = OrderHistoryQuery.Owned(db.Orders.AsNoTracking(), User.Identity.Name);
            var model = new MyOrderVM
            {
                Q = q,
                SelectedStatus = status,
                HasAnyOrders = owned.Any(),
                Tabs = new List<MyOrderTabVM>
                {
                    new MyOrderTabVM { Key = "all", Label = "Tất cả" },
                    new MyOrderTabVM { Key = "awaiting-payment", Label = "Chờ thanh toán" },
                    new MyOrderTabVM { Key = "processing", Label = "Đang xử lý" },
                    new MyOrderTabVM { Key = "shipping", Label = "Đang vận chuyển" },
                    new MyOrderTabVM { Key = "delivered", Label = "Đã giao" },
                    new MyOrderTabVM { Key = "cancelled", Label = "Đã hủy" }
                }
            };
            if (!model.HasAnyOrders) return View("MyOrder", model);

            var searched = OrderHistoryQuery.Search(owned, q);
            model.MatchedCount = searched.Count();
            foreach (var tab in model.Tabs)
            {
                tab.Count = tab.Key == "all" ? model.MatchedCount : OrderHistoryQuery.Bucket(searched, tab.Key).Count();
                if (tab.Key == "all" || tab.Count == 0 || (status != "all" && status != tab.Key)) continue;
                var isExpanded = expanded == tab.Key;
                // Apply the limit before loading details/products; never materialize the whole history.
                var orders = OrderHistoryQuery.Slice(searched, tab.Key, isExpanded)
                    .Include(order => order.OrderDetails.Select(line => line.Product)).ToList();
                model.Groups.Add(new MyOrderGroupVM
                {
                    Key = tab.Key,
                    Label = tab.Label,
                    TotalCount = tab.Count,
                    IsExpanded = isExpanded,
                    Orders = orders
                });
            }
            return View("MyOrder", model);
        }
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "C")]
        public ActionResult Reorder(int id, string requestKey, string returnUrl = null)
        {
            string key;
            if (!ModelState.IsValid || id <= 0 || !CartRules.TryNormalizeReorderKey(requestKey, out key))
                return ReorderResult(false, "Yêu cầu mua lại không hợp lệ. Vui lòng tải lại trang đơn hàng và thử lại.", returnUrl);

            // The browser supplies only the order ID; stored lines and ownership come from SQL.
            var order = OwnedOrder(id);
            if (order == null)
                return ReorderResult(false, "Không tìm thấy đơn hàng của bạn để mua lại.", returnUrl);

            const string ledgerName = "SuccessfulReorderRequests";
            var receipts = Session[ledgerName] as List<ReorderReceipt> ?? new List<ReorderReceipt>();
            var previous = receipts.FirstOrDefault(receipt => receipt != null && receipt.RequestKey == key);
            if (previous != null)
            {
                if (previous.OrderId != id || !string.Equals(previous.Username, User.Identity.Name, StringComparison.Ordinal))
                    return ReorderResult(false, "Yêu cầu mua lại này đã được dùng cho đơn hàng khác. Vui lòng tải lại trang trước khi mua lại.", returnUrl);
                return ReorderResult(true, "Đơn hàng đã được thêm vào giỏ hàng trước đó; không cộng thêm lần nữa.", returnUrl);
            }

            var storedCart = Session["Cart"];
            var current = storedCart as Dictionary<int, int>;
            if (storedCart != null && current == null)
                return ReorderResult(false, "Giỏ hàng hiện tại không hợp lệ. Vui lòng kiểm tra giỏ hàng trước khi mua lại.", returnUrl);
            current = current ?? new Dictionary<int, int>();
            Dictionary<int, int> updated;
            string message;
            if (!CartRules.TryBuildReorder(current, order.OrderDetails, out updated, out message))
                return ReorderResult(false, message, returnUrl);

            // ASP.NET's writable session lock serializes competing requests in this session.
            // A failed item never changes the original cart or consumes its request key.
            var nextReceipts = new List<ReorderReceipt>(receipts);
            nextReceipts.Add(new ReorderReceipt { RequestKey = key, OrderId = id, Username = User.Identity.Name });
            while (nextReceipts.Count > 32) nextReceipts.RemoveAt(0);
            Session["Cart"] = updated;
            Session[ledgerName] = nextReceipts;
            return ReorderResult(true, message, returnUrl);
        }

        private ActionResult ReorderResult(bool success, string message, string returnUrl)
        {
            var cart = Session["Cart"] as Dictionary<int, int>;
            long count = cart == null ? 0L : cart.Values.Where(quantity => quantity > 0).Sum(quantity => (long)quantity);
            int cartCount = (int)Math.Min(int.MaxValue, count);
            if (Request.IsAjaxRequest()) return Json(new { success, message, cartCount });

            TempData[success ? "Success" : "Error"] = message;
            if (success) return RedirectToAction("Index", "Cart");
            return Url.IsLocalUrl(returnUrl) ? (ActionResult)Redirect(returnUrl) : RedirectToAction("History");
        }

        [Serializable]
        private sealed class ReorderReceipt
        {
            public string RequestKey { get; set; }
            public int OrderId { get; set; }
            public string Username { get; set; }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var customer = db.Customers.SingleOrDefault(x => x.Username == User.Identity.Name);
            if (customer == null) return HttpNotFound();
            try
            {
                using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    // Ownership and state are checked under the same lock as admin updates.
                    var order = db.Orders.SqlQuery(
                        "SELECT * FROM [dbo].[Order] WITH (UPDLOCK,HOLDLOCK) WHERE [OrderID]=@p0 AND [CustomerID]=@p1",
                        id, customer.CustomerID).SingleOrDefault();
                    if (order == null) return HttpNotFound();
                    if (order.OrderStatus == "Cancelled")
                    {
                        TempData["Success"] = "Đơn hàng đã được hủy trước đó.";
                        return RedirectToAction("Details", new { id });
                    }
                    if (!PaymentRules.CanCustomerCancel(order))
                    {
                        TempData["Error"] = "Chỉ có thể tự hủy khi đơn còn chờ xử lý. Vui lòng liên hệ cửa hàng nếu cần hỗ trợ.";
                        return RedirectToAction("Details", new { id });
                    }
                    var lines = db.OrderDetails.Where(x => x.OrderID == id).OrderBy(x => x.ProductID).ToList();
                    foreach (var line in lines)
                    {
                        db.Database.ExecuteSqlCommand(
                            "UPDATE [dbo].[Product] SET [StockQuantity]=[StockQuantity]+@p0 WHERE [ProductID]=@p1",
                            line.Quantity, line.ProductID);
                    }
                    order.OrderStatus = "Cancelled";
                    // Cancellation never erases a payment record or triggers a bank refund.
                    db.SaveChanges();
                    tx.Commit();
                    TempData["Success"] = order.PaymentStatus == "Paid"
                        ? "Đã hủy đơn hàng. Đơn đã thanh toán: vui lòng liên hệ cửa hàng để đối chiếu và xử lý hoàn tiền."
                        : order.PaymentMethod == "BankTransfer"
                            ? "Đã hủy đơn hàng. Nếu bạn đã chuyển khoản, vui lòng liên hệ cửa hàng để đối chiếu giao dịch."
                            : "Đã hủy đơn hàng thành công.";
                }
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Error"] = "Chưa thể hủy đơn hàng. Vui lòng tải lại trang và thử lại.";
            }
            catch (SqlException)
            {
                TempData["Error"] = "Đơn hàng đang được cập nhật. Vui lòng tải lại trang và kiểm tra trạng thái trước khi thử lại.";
            }
            return RedirectToAction("Details", new { id });
        }
        private Order OwnedOrder(int id)
        {
            return db.Orders.AsNoTracking().Include(x=>x.Customer).Include(x=>x.OrderDetails.Select(d=>d.Product)).SingleOrDefault(x=>x.OrderID==id && x.Customer.Username==User.Identity.Name);
        }
        private void PopulatePayment(CheckoutVM model)
        {
            model.BankTransferAvailable = BankTransferService.IsAvailable(Server);
            model.BankQrImageUrl = model.BankTransferAvailable ? BankTransferService.QrImagePath : null;
        }
        private void PopulateOrderPayment()
        {
            ViewBag.BankTransferAvailable = BankTransferService.IsAvailable(Server);
            ViewBag.BankQrImageUrl = ViewBag.BankTransferAvailable ? BankTransferService.QrImagePath : null;
        }
        public ActionResult Details(int id) { var order=OwnedOrder(id); if (order==null) return HttpNotFound(); PopulateOrderPayment(); return View(order); }
        public ActionResult Success(int id) { var order=OwnedOrder(id); if (order==null) return HttpNotFound(); PopulateOrderPayment(); return View(order); }
        protected override void Dispose(bool disposing) { if(disposing) db.Dispose(); base.Dispose(disposing); }
    }

    // Pure IQueryable helpers keep filtering in SQL and allow focused query tests.
    public static class OrderHistoryQuery
    {
        public const int SearchLimit = 200;
        public const int PreviewLimit = 2;
        public const int ExpandedLimit = 50;

        public static string NormalizeSearch(string q)
        {
            q = (q ?? "").Trim();
            return q.Length <= SearchLimit ? q : q.Substring(0, SearchLimit).TrimEnd();
        }

        public static string NormalizeStatus(string status)
        {
            var key = (status ?? "all").Trim().ToLowerInvariant();
            switch (key)
            {
                case "all": case "awaiting-payment": case "processing":
                case "shipping": case "delivered": case "cancelled": return key;
                default: return "all";
            }
        }

        public static string NormalizeExpanded(string expanded)
        {
            var key = NormalizeStatus(expanded);
            return key == "all" ? null : key;
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

        public static IQueryable<Order> Owned(IQueryable<Order> orders, string username)
        {
            if (string.IsNullOrEmpty(username)) return orders.Where(order => false);
            return orders.Where(order => order.Customer.Username == username);
        }

        public static IQueryable<Order> Search(IQueryable<Order> ownedOrders, string q)
        {
            q = NormalizeSearch(q);
            if (q.Length == 0) return ownedOrders;
            var parsedId = ParseOrderId(q);
            if (parsedId.HasValue)
            {
                var id = parsedId.Value;
                return ownedOrders.Where(order => order.OrderID == id ||
                    order.OrderDetails.Any(line => line.Product.ProductName.Contains(q)));
            }
            return ownedOrders.Where(order => order.OrderDetails.Any(line => line.Product.ProductName.Contains(q)));
        }

        public static IQueryable<Order> Bucket(IQueryable<Order> ownedOrders, string key)
        {
            switch (key)
            {
                case "all": return ownedOrders;
                case "awaiting-payment":
                    return ownedOrders.Where(order => (order.OrderStatus == "Pending" || order.OrderStatus == "Processing") &&
                        order.PaymentMethod == "BankTransfer" && order.PaymentStatus == "Unpaid");
                case "processing":
                    return ownedOrders.Where(order => (order.OrderStatus == "Pending" || order.OrderStatus == "Processing") &&
                        (order.PaymentMethod != "BankTransfer" || order.PaymentStatus != "Unpaid"));
                case "shipping": return ownedOrders.Where(order => order.OrderStatus == "Shipping");
                case "delivered": return ownedOrders.Where(order => order.OrderStatus == "Delivered");
                case "cancelled": return ownedOrders.Where(order => order.OrderStatus == "Cancelled");
                default: return ownedOrders.Where(order => false);
            }
        }

        public static IQueryable<Order> Slice(IQueryable<Order> ownedOrders, string key, bool isExpanded)
        {
            return Bucket(ownedOrders, key).OrderByDescending(order => order.OrderDate)
                .ThenByDescending(order => order.OrderID).Take(isExpanded ? ExpandedLimit : PreviewLimit);
        }
    }
}

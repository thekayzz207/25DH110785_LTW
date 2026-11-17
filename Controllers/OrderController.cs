using System;
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
        public ActionResult History()
        {
            return View(db.Orders.AsNoTracking().Where(x=>x.Customer.Username==User.Identity.Name).OrderByDescending(x=>x.OrderID).ToList());
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
}

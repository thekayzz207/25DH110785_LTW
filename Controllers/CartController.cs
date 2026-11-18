using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Models;
using LTW_25DH110785.Services;

namespace LTW_25DH110785.Controllers
{
    public class CartController : Controller
    {
        private readonly StoreDbContext db = new StoreDbContext();
        public ActionResult Index() { return View(CartService.Read(db,Session)); }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Add(int productId, int quantity = 1, string returnUrl = null)
        {
            var cart = CartService.Get(Session);
            bool success = false;
            string message = "Thông tin sản phẩm hoặc số lượng không hợp lệ. Vui lòng kiểm tra lại.";
            if (ModelState.IsValid)
                success = CartRules.TryAdd(cart, db.Products.Find(productId), quantity, out message);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success, message, cartCount = cart.Values.Sum() });
            }

            TempData[success ? "Success" : "Error"] = message;
            return Url.IsLocalUrl(returnUrl) ? (ActionResult)Redirect(returnUrl) : RedirectToAction("Index");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Update(int productId, int? quantity = null)
        {
            var cart = CartService.Get(Session);
            bool success = false;
            string message = "Nhập số lượng hợp lệ.";
            if (ModelState.IsValid && quantity.HasValue)
                success = CartRules.TryUpdate(cart, productId, db.Products.Find(productId), quantity.Value, out message);
            if (Request.IsAjaxRequest()) return CartSnapshot(success, message);
            if (!success) TempData["Error"] = message;
            return RedirectToAction("Index");
        }
        private JsonResult CartSnapshot(bool success, string message)
        {
            var cart = CartService.Read(db, Session);
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            return Json(new {
                success,
                message,
                cartCount = cart.Items.Sum(x => x.Quantity),
                totalCents = decimal.ToInt64(cart.Total * 100m),
                totalText = cart.Total.ToString("N0", vi) + " ₫",
                items = cart.Items.Select(x => new {
                    productId = x.ProductID,
                    quantity = x.Quantity,
                    stockQuantity = Math.Max(0, x.StockQuantity),
                    maxQuantity = Math.Min(99, Math.Max(0, x.StockQuantity)),
                    unitPriceCents = decimal.ToInt64(x.Price * 100m),
                    lineTotalCents = decimal.ToInt64(x.Subtotal * 100m),
                    lineTotalText = x.Subtotal.ToString("N0", vi) + " ₫",
                    available = x.StockQuantity > 0
                }).ToArray()
            });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Remove(int productId) { CartService.Get(Session).Remove(productId); return RedirectToAction("Index"); }
        protected override void Dispose(bool disposing) { if(disposing) db.Dispose(); base.Dispose(disposing); }
    }
}

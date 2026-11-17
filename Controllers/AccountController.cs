using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using LTW_25DH110785.Models;
using LTW_25DH110785.Services;
using LTW_25DH110785.ViewModels;

namespace LTW_25DH110785.Controllers
{
    public class AccountController : Controller
    {
        private readonly StoreDbContext db = new StoreDbContext();
        [AllowAnonymous] public ActionResult Login(string returnUrl) { return View(new LoginVM { ReturnUrl=returnUrl }); }
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(LoginVM model)
        {
            if (!ModelState.IsValid) return View(model);
            var key = "login:" + (Request.UserHostAddress ?? "local") + ":" + model.Username.ToLowerInvariant();
            int attempts = (HttpRuntime.Cache[key] as int?) ?? 0;
            if (attempts >= 10) { ModelState.AddModelError("", "Bạn đã thử nhiều lần. Vui lòng thử lại sau 15 phút."); return View(model); }
            var user = db.Users.Find(model.Username);
            if (user == null || !PasswordService.Verify(model.Password, user.Password))
            {
                HttpRuntime.Cache.Insert(key, attempts + 1, null, DateTime.UtcNow.AddMinutes(15), System.Web.Caching.Cache.NoSlidingExpiration);
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu chưa đúng."); return View(model);
            }
            HttpRuntime.Cache.Remove(key);
            FormsAuthentication.SetAuthCookie(user.Username, false);
            if (Url.IsLocalUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl);
            return user.UserRole == "A" ? RedirectToAction("Index", "Home", new { area="Admin" }) : RedirectToAction("Index", "Home");
        }
        [AllowAnonymous] public ActionResult Register() { return View(new RegisterVM()); }
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterVM model)
        {
            if (!ModelState.IsValid) return View(model);
            if (db.Users.Any(x=>x.Username==model.Username)) { ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng."); return View(model); }
            using (var tx = db.Database.BeginTransaction())
            {
                db.Users.Add(new User { Username=model.Username, Password=PasswordService.Hash(model.Password), UserRole="C" });
                db.Customers.Add(new Customer { Username=model.Username, CustomerName=model.CustomerName.Trim(), CustomerPhone=model.CustomerPhone, CustomerEmail=model.CustomerEmail, CustomerAddress=model.CustomerAddress });
                try { db.SaveChanges(); tx.Commit(); }
                catch (DbUpdateException) { tx.Rollback(); ModelState.AddModelError("", "Không thể tạo tài khoản. Tên đăng nhập có thể đã được sử dụng."); return View(model); }
            }
            FormsAuthentication.SetAuthCookie(model.Username, false);
            TempData["Success"] = "Tạo tài khoản thành công. Chào mừng bạn đến AnhKhoaStore!";
            return RedirectToAction("Index", "Home");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout() { FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon(); return RedirectToAction("Index", "Home"); }
        [Authorize(Roles="C")] public new ActionResult Profile()
        {
            var c = db.Customers.SingleOrDefault(x=>x.Username==User.Identity.Name); if (c==null) return HttpNotFound();
            return View(new ProfileVM { CustomerName=c.CustomerName,CustomerPhone=c.CustomerPhone,CustomerEmail=c.CustomerEmail,CustomerAddress=c.CustomerAddress });
        }
        [HttpPost, Authorize(Roles="C"), ValidateAntiForgeryToken]
        public new ActionResult Profile(ProfileVM model)
        {
            if (!ModelState.IsValid) return View(model);
            var c = db.Customers.SingleOrDefault(x=>x.Username==User.Identity.Name); if (c==null) return HttpNotFound();
            c.CustomerName=model.CustomerName.Trim(); c.CustomerPhone=model.CustomerPhone; c.CustomerEmail=model.CustomerEmail; c.CustomerAddress=model.CustomerAddress;
            db.SaveChanges(); TempData["Success"]="Đã cập nhật hồ sơ."; return RedirectToAction("Profile");
        }
        [Authorize] public ActionResult ChangePassword() { return View(new ChangePasswordVM()); }
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);
            var user=db.Users.Find(User.Identity.Name); if(user==null) return HttpNotFound();
            if (!PasswordService.Verify(model.CurrentPassword,user.Password)) { ModelState.AddModelError("CurrentPassword","Mật khẩu hiện tại chưa đúng."); return View(model); }
            user.Password=PasswordService.Hash(model.NewPassword); db.SaveChanges();
            FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon();
            TempData["Success"]="Đã đổi mật khẩu. Vui lòng đăng nhập lại."; return RedirectToAction("Login");
        }
        protected override void Dispose(bool disposing) { if(disposing) db.Dispose(); base.Dispose(disposing); }
    }
}

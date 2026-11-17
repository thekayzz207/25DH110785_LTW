using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Areas.Admin.Models;
using LTW_25DH110785.Models;
using LTW_25DH110785.Services;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class UsersController : AdminController
    {
        public ActionResult Index(string q, string sort = "name", int page = 1)
        {
            q = (q ?? "").Trim();
            var query = db.Users.AsNoTracking().Where(u => u.UserRole == "A");
            if (q.Length > 0) query = query.Where(u => u.Username.Contains(q));
            query = sort == "name_desc" ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username);
            return View(Paginate(query, page, q, sort));
        }
        public ActionResult Create()
        {
            ViewBag.IsEdit = false;
            return View("Form", new AdminUserVM());
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(AdminUserVM model)
        {
            if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError("Password", "Vui lòng nhập mật khẩu từ 10 ký tự.");
            if (db.Users.Any(u => u.Username == model.Username)) ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng.");
            if (ModelState.IsValid)
            {
                db.Users.Add(new User { Username = model.Username, Password = PasswordService.Hash(model.Password), UserRole = "A" });
                try
                {
                    db.SaveChanges();
                    TempData["Success"] = "Đã tạo tài khoản quản trị.";
                    return RedirectToAction("Index");
                }
                catch (DbUpdateException) { ModelState.AddModelError("", "Không thể tạo tài khoản. Tên đăng nhập có thể vừa được sử dụng."); }
            }
            ClearPasswords(model);
            ViewBag.IsEdit = false;
            return View("Form", model);
        }
        public ActionResult Edit(string id)
        {
            var user = db.Users.AsNoTracking().SingleOrDefault(u => u.Username == id && u.UserRole == "A");
            if (user == null) return HttpNotFound();
            ViewBag.IsEdit = true;
            return View("Form", new AdminUserVM { Username = user.Username });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(AdminUserVM model)
        {
            // Both lookup and update remain restricted to administrators.
            var user = db.Users.SingleOrDefault(u => u.Username == model.Username && u.UserRole == "A");
            if (user == null) return HttpNotFound();
            if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(model.Password)) user.Password = PasswordService.Hash(model.Password);
                db.SaveChanges();
                TempData["Success"] = "Đã cập nhật tài khoản quản trị.";
                return RedirectToAction("Index");
            }
            ClearPasswords(model);
            ViewBag.IsEdit = true;
            return View("Form", model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            if (string.Equals(id, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Bạn không thể xóa tài khoản đang đăng nhập.";
                return RedirectToAction("Index");
            }
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    // Lock the administrator set before counting to preserve at least one admin.
                    var admins = db.Users.SqlQuery("SELECT * FROM [dbo].[User] WITH (UPDLOCK,HOLDLOCK) WHERE [UserRole] = N'A'").ToList();
                    var user = admins.SingleOrDefault(u => string.Equals(u.Username, id, StringComparison.OrdinalIgnoreCase));
                    if (user == null) return HttpNotFound();
                    if (admins.Count <= 1)
                    {
                        TempData["Error"] = "Phải giữ lại ít nhất một tài khoản quản trị.";
                        return RedirectToAction("Index");
                    }
                    db.Users.Remove(user);
                    db.SaveChanges();
                    transaction.Commit();
                    TempData["Success"] = "Đã xóa tài khoản quản trị.";
                }
            }
            catch (DbUpdateException) { TempData["Error"] = "Tài khoản đang liên kết với dữ liệu khác, không thể xóa."; }
            catch (System.Data.SqlClient.SqlException) { TempData["Error"] = "Tài khoản đang được cập nhật. Hãy tải lại rồi thử lại."; }
            return RedirectToAction("Index");
        }
        private void ClearPasswords(AdminUserVM model)
        {
            model.Password = null;
            model.ConfirmPassword = null;
            if (ModelState.ContainsKey("Password")) ModelState["Password"].Value = new ValueProviderResult(null, "", System.Globalization.CultureInfo.CurrentCulture);
            if (ModelState.ContainsKey("ConfirmPassword")) ModelState["ConfirmPassword"].Value = new ValueProviderResult(null, "", System.Globalization.CultureInfo.CurrentCulture);
        }
    }
}

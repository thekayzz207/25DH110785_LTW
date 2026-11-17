using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Areas.Admin.Models;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class CategoriesController : AdminController
    {
        public ActionResult Index()
        {
            return View(db.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToList());
        }
        public ActionResult Create()
        {
            ViewBag.IsEdit = false;
            return View("Form", new CategoryFormVM());
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(CategoryFormVM model)
        {
            model.CategoryName = model.CategoryName == null ? null : model.CategoryName.Trim();
            if (string.IsNullOrWhiteSpace(model.CategoryName)) ModelState.AddModelError("CategoryName", "Vui lòng nhập tên danh mục.");
            if (db.Categories.Any(c => c.CategoryName == model.CategoryName)) ModelState.AddModelError("CategoryName", "Danh mục này đã tồn tại.");
            if (ModelState.IsValid)
            {
                db.Categories.Add(new Category { CategoryName = model.CategoryName });
                db.SaveChanges();
                TempData["Success"] = "Đã thêm danh mục.";
                return RedirectToAction("Index");
            }
            ViewBag.IsEdit = false;
            return View("Form", model);
        }
        public ActionResult Edit(int id)
        {
            var category = db.Categories.Find(id);
            if (category == null) return HttpNotFound();
            ViewBag.IsEdit = true;
            return View("Form", new CategoryFormVM { CategoryID = category.CategoryID, CategoryName = category.CategoryName });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(CategoryFormVM model)
        {
            var category = db.Categories.Find(model.CategoryID);
            if (category == null) return HttpNotFound();
            model.CategoryName = model.CategoryName == null ? null : model.CategoryName.Trim();
            if (string.IsNullOrWhiteSpace(model.CategoryName)) ModelState.AddModelError("CategoryName", "Vui lòng nhập tên danh mục.");
            if (db.Categories.Any(c => c.CategoryName == model.CategoryName && c.CategoryID != model.CategoryID)) ModelState.AddModelError("CategoryName", "Danh mục này đã tồn tại.");
            if (ModelState.IsValid)
            {
                category.CategoryName = model.CategoryName;
                db.SaveChanges();
                TempData["Success"] = "Đã cập nhật danh mục.";
                return RedirectToAction("Index");
            }
            ViewBag.IsEdit = true;
            return View("Form", model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var category = db.Categories.Find(id);
            if (category == null) return HttpNotFound();
            if (db.Products.Any(p => p.CategoryID == id))
                TempData["Error"] = "Danh mục đang có sản phẩm. Hãy chuyển các sản phẩm sang danh mục khác trước khi xóa.";
            else
            {
                try
                {
                    db.Categories.Remove(category);
                    db.SaveChanges();
                    TempData["Success"] = "Đã xóa danh mục.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Danh mục đã được sử dụng trong dữ liệu khác, không thể xóa.";
                }
            }
            return RedirectToAction("Index");
        }
    }
}

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using LTW_25DH110785.Areas.Admin.Models;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class ProductsController : AdminController
    {
        public ActionResult Index(string q, string sort = "newest", int page = 1)
        {
            q = (q ?? "").Trim();
            var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            if (q.Length > 0) query = query.Where(p => p.ProductName.Contains(q) || p.Brand.Contains(q));
            switch (sort)
            {
                case "name": query = query.OrderBy(p => p.ProductName).ThenBy(p => p.ProductID); break;
                case "price_asc": query = query.OrderBy(p => p.ProductPrice).ThenBy(p => p.ProductID); break;
                case "price_desc": query = query.OrderByDescending(p => p.ProductPrice).ThenBy(p => p.ProductID); break;
                case "stock": query = query.OrderBy(p => p.StockQuantity).ThenBy(p => p.ProductID); break;
                default: sort = "newest"; query = query.OrderByDescending(p => p.ProductID); break;
            }
            return View(Paginate(query, page, q, sort));
        }
        public ActionResult Create()
        {
            PrepareForm(false);
            return View("Form", new ProductFormVM());
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(ProductFormVM model)
        {
            ValidateProduct(model);
            string image = null;
            if (ModelState.IsValid) image = SaveImage(model.ImageFile);
            if (ModelState.IsValid)
            {
                var product = new Product();
                Apply(product, model);
                product.ProductImage = image ?? "~/Content/images/cpu.svg";
                db.Products.Add(product);
                try
                {
                    db.SaveChanges();
                    TempData["Success"] = "Đã thêm sản phẩm.";
                    return RedirectToAction("Index");
                }
                catch (DbUpdateException)
                {
                    DeleteUploadedImage(image);
                    ModelState.AddModelError("", "Không thể lưu sản phẩm. Hãy kiểm tra danh mục rồi thử lại.");
                }
            }
            PrepareForm(false, model.CategoryID);
            return View("Form", model);
        }
        public ActionResult Edit(int id)
        {
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();
            PrepareForm(true, product.CategoryID);
            return View("Form", new ProductFormVM
            {
                ProductID = product.ProductID, CategoryID = product.CategoryID,
                RowVersionToken = Convert.ToBase64String(product.RowVersion),
                ProductName = product.ProductName, ProductDecription = product.ProductDecription,
                ProductPrice = product.ProductPrice, ProductImage = product.ProductImage,
                Brand = product.Brand, DeviceType = product.DeviceType,
                StockQuantity = product.StockQuantity, Specifications = product.Specifications, IsActive = product.IsActive
            });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(ProductFormVM model)
        {
            var product = db.Products.Find(model.ProductID);
            if (product == null) return HttpNotFound();
            // The existing image path always comes from SQL, never a hidden form field.
            model.ProductImage = product.ProductImage;
            ValidateProduct(model);
            byte[] originalVersion = null;
            try
            {
                originalVersion = Convert.FromBase64String(model.RowVersionToken ?? "");
                if (originalVersion.Length != 8) ModelState.AddModelError("", "Phiên chỉnh sửa không hợp lệ. Hãy tải lại trang sản phẩm trước khi lưu.");
            }
            catch (FormatException) { ModelState.AddModelError("", "Phiên chỉnh sửa không hợp lệ. Hãy tải lại trang sản phẩm trước khi lưu."); }
            string image = null;
            if (ModelState.IsValid) image = SaveImage(model.ImageFile);
            if (ModelState.IsValid)
            {
                Apply(product, model);
                db.Entry(product).Property(p => p.RowVersion).OriginalValue = originalVersion;
                if (image != null) product.ProductImage = image;
                try
                {
                    db.SaveChanges();
                    TempData["Success"] = "Đã cập nhật sản phẩm.";
                    return RedirectToAction("Index");
                }
                catch (DbUpdateConcurrencyException)
                {
                    DeleteUploadedImage(image);
                    TempData["Error"] = "Sản phẩm vừa có thay đổi hoặc đơn hàng mới. Dữ liệu đã được tải lại; hãy kiểm tra tồn kho và nhập lại thay đổi trước khi lưu.";
                    return RedirectToAction("Edit", new { id = model.ProductID });
                }
                catch (DbUpdateException)
                {
                    DeleteUploadedImage(image);
                    ModelState.AddModelError("", "Không thể lưu sản phẩm. Hãy kiểm tra danh mục rồi thử lại.");
                }
            }
            PrepareForm(true, model.CategoryID);
            return View("Form", model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();
            // Keep order history and historical unit prices intact.
            if (db.OrderDetails.Any(d => d.ProductID == id))
            {
                product.IsActive = false;
                try
                {
                    db.SaveChanges();
                    TempData["Success"] = "Sản phẩm đã có trong đơn hàng nên được ngừng bán để giữ lịch sử.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    TempData["Error"] = "Sản phẩm vừa được cập nhật. Hãy tải lại danh sách trước khi ngừng bán.";
                }
            }
            else
            {
                try
                {
                    db.Products.Remove(product);
                    db.SaveChanges();
                    TempData["Success"] = "Đã xóa sản phẩm.";
                }
                catch (DbUpdateException)
                {
                    TempData["Error"] = "Sản phẩm vừa được sử dụng trong đơn hàng. Hãy tải lại và ngừng bán sản phẩm.";
                }
            }
            return RedirectToAction("Index");
        }
        private void PrepareForm(bool isEdit, int? categoryId = null)
        {
            ViewBag.IsEdit = isEdit;
            ViewBag.Categories = new SelectList(db.Categories.OrderBy(c => c.CategoryName).ToList(), "CategoryID", "CategoryName", categoryId);
        }
        private void ValidateProduct(ProductFormVM model)
        {
            if (!db.Categories.Any(c => c.CategoryID == model.CategoryID)) ModelState.AddModelError("CategoryID", "Danh mục không tồn tại.");
            if (string.IsNullOrWhiteSpace(model.ProductName)) ModelState.AddModelError("ProductName", "Vui lòng nhập tên sản phẩm.");
            if (string.IsNullOrWhiteSpace(model.Brand)) ModelState.AddModelError("Brand", "Vui lòng nhập thương hiệu.");
            if (string.IsNullOrWhiteSpace(model.ProductDecription)) ModelState.AddModelError("ProductDecription", "Vui lòng nhập mô tả.");
        }
        private static void Apply(Product product, ProductFormVM model)
        {
            product.CategoryID = model.CategoryID;
            product.ProductName = model.ProductName.Trim();
            product.ProductDecription = model.ProductDecription.Trim();
            product.ProductPrice = model.ProductPrice;
            product.Brand = model.Brand.Trim();
            product.DeviceType = model.DeviceType;
            product.StockQuantity = model.StockQuantity;
            product.Specifications = model.Specifications ?? "";
            product.IsActive = model.IsActive;
        }
        private string SaveImage(HttpPostedFileBase upload)
        {
            if (upload == null || upload.ContentLength == 0) return null;
            if (upload.ContentLength > 2 * 1024 * 1024)
            {
                ModelState.AddModelError("ImageFile", "Ảnh phải nhỏ hơn hoặc bằng 2 MB.");
                return null;
            }
            string extension;
            try { extension = Path.GetExtension(upload.FileName ?? "").ToLowerInvariant(); }
            catch (ArgumentException)
            {
                ModelState.AddModelError("ImageFile", "Tên tập tin ảnh không hợp lệ.");
                return null;
            }
            var header = new byte[12];
            int count = upload.InputStream.Read(header, 0, header.Length);
            bool png = count >= 8 && header[0] == 137 && header[1] == 80 && header[2] == 78 && header[3] == 71 && header[4] == 13 && header[5] == 10 && header[6] == 26 && header[7] == 10;
            bool jpeg = count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
            bool webp = count >= 12 && header[0] == 82 && header[1] == 73 && header[2] == 70 && header[3] == 70 && header[8] == 87 && header[9] == 69 && header[10] == 66 && header[11] == 80;
            bool valid = (png && extension == ".png") || (jpeg && (extension == ".jpg" || extension == ".jpeg")) || (webp && extension == ".webp");
            if (!valid || !upload.InputStream.CanSeek)
            {
                ModelState.AddModelError("ImageFile", "Chỉ nhận ảnh PNG, JPG hoặc WebP có nội dung đúng định dạng.");
                return null;
            }
            upload.InputStream.Position = 0;
            string relativePath = "~/Content/uploads/" + Guid.NewGuid().ToString("N") + extension;
            try
            {
                var path = Server.MapPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                upload.SaveAs(path);
                return relativePath;
            }
            catch (IOException)
            {
                ModelState.AddModelError("ImageFile", "Không lưu được ảnh. Kiểm tra quyền ghi thư mục Content/uploads.");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                ModelState.AddModelError("ImageFile", "Ứng dụng chưa có quyền ghi vào Content/uploads.");
                return null;
            }
        }
        private void DeleteUploadedImage(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            try { System.IO.File.Delete(Server.MapPath(relativePath)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

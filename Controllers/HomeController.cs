using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Models;
using LTW_25DH110785.ViewModels;

namespace LTW_25DH110785.Controllers
{
    public class HomeController : Controller
    {
        private readonly StoreDbContext db = new StoreDbContext();
        public ActionResult Index(string q, int? categoryId, string device, string sort = "newest", decimal? minPrice = null, decimal? maxPrice = null, int page = 1)
        {
            q = (q ?? "").Trim(); if (q.Length > 150) q = q.Substring(0,150);
            if (device != "PC" && device != "Laptop") device = "";
            var query = db.Products.AsNoTracking().Include(x => x.Category).Where(x => x.IsActive);
            if (q.Length > 0) query = query.Where(x => x.ProductName.Contains(q) || x.Brand.Contains(q) || x.ProductDecription.Contains(q));
            if (categoryId.HasValue) query = query.Where(x => x.CategoryID == categoryId.Value);
            if (device.Length > 0) query = query.Where(x => x.DeviceType == device || x.DeviceType == "Both");
            if (minPrice.HasValue && minPrice.Value >= 0) query = query.Where(x => x.ProductPrice >= minPrice.Value);
            if (maxPrice.HasValue && maxPrice.Value >= 0) query = query.Where(x => x.ProductPrice <= maxPrice.Value);
            switch (sort)
            {
                case "price-asc": query = query.OrderBy(x => x.ProductPrice).ThenBy(x => x.ProductID); break;
                case "price-desc": query = query.OrderByDescending(x => x.ProductPrice).ThenBy(x => x.ProductID); break;
                case "name": query = query.OrderBy(x => x.ProductName).ThenBy(x => x.ProductID); break;
                default: sort = "newest"; query = query.OrderByDescending(x => x.ProductID); break;
            }
            const int size = 9; int total = query.Count();
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)size)); page = Math.Max(1, Math.Min(page,pages));
            return View(new CatalogVM { Products=query.Skip((page-1)*size).Take(size).ToList(), Categories=db.Categories.AsNoTracking().OrderBy(x=>x.CategoryName).ToList(), Q=q, CategoryId=categoryId, Device=device, Sort=sort, MinPrice=minPrice, MaxPrice=maxPrice, Page=page, TotalPages=pages, TotalItems=total });
        }
        public ActionResult Details(int id)
        {
            var product = db.Products.AsNoTracking().Include(x=>x.Category).SingleOrDefault(x=>x.ProductID==id && x.IsActive);
            if (product == null) return HttpNotFound();
            return View(new ProductDetailVM { Product=product, RelatedProducts=db.Products.AsNoTracking().Where(x=>x.IsActive && x.CategoryID==product.CategoryID && x.ProductID!=id).OrderBy(x=>x.ProductPrice).Take(3).ToList() });
        }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}

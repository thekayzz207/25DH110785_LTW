using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Areas.Admin.Models;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    [Authorize(Roles = "A")]
    public abstract class AdminController : Controller
    {
        protected readonly StoreDbContext db = new StoreDbContext();
        protected PagedListVM<T> Paginate<T>(IQueryable<T> query, int page, string q, string sort)
        {
            const int pageSize = 10;
            var count = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));
            return new PagedListVM<T>
            {
                Items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page, TotalPages = totalPages, TotalItems = count, Q = q, Sort = sort
            };
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}

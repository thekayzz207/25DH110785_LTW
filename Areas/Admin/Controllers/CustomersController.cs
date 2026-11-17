using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class CustomersController : AdminController
    {
        public ActionResult Index(string q, string sort = "newest", int page = 1)
        {
            q = (q ?? "").Trim();
            var query = db.Customers.AsNoTracking().AsQueryable();
            if (q.Length > 0) query = query.Where(c => c.CustomerName.Contains(q) || c.Username.Contains(q) || c.CustomerPhone.Contains(q) || c.CustomerEmail.Contains(q));
            switch (sort)
            {
                case "name": query = query.OrderBy(c => c.CustomerName).ThenBy(c => c.CustomerID); break;
                case "name_desc": query = query.OrderByDescending(c => c.CustomerName).ThenBy(c => c.CustomerID); break;
                case "username": query = query.OrderBy(c => c.Username).ThenBy(c => c.CustomerID); break;
                default: sort = "newest"; query = query.OrderByDescending(c => c.CustomerID); break;
            }
            return View(Paginate(query, page, q, sort));
        }
        public ActionResult Details(int id)
        {
            var customer = db.Customers.AsNoTracking().SingleOrDefault(c => c.CustomerID == id);
            if (customer == null) return HttpNotFound();
            ViewBag.OrderCount = db.Orders.Count(o => o.CustomerID == id);
            return View(customer);
        }
    }
}

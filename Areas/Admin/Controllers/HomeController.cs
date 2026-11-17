using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using LTW_25DH110785.Areas.Admin.Models;

namespace LTW_25DH110785.Areas.Admin.Controllers
{
    public class HomeController : AdminController
    {
        public ActionResult Index()
        {
            return View(new DashboardVM
            {
                ProductCount = db.Products.Count(),
                OrderCount = db.Orders.Count(),
                CustomerCount = db.Customers.Count(),
                Revenue = db.Orders.Where(o => o.OrderStatus == "Delivered").Select(o => (decimal?)o.TotalAmount).Sum() ?? 0m,
                RecentOrders = db.Orders.AsNoTracking().Include(o => o.Customer).OrderByDescending(o => o.OrderID).Take(6).ToList()
            });
        }
    }
}

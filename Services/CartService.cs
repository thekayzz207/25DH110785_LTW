using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Web;
using LTW_25DH110785.Models;
using LTW_25DH110785.ViewModels;

namespace LTW_25DH110785.Services
{
    public static class CartService
    {
        public static Dictionary<int, int> Get(HttpSessionStateBase session)
        {
            var cart = session["Cart"] as Dictionary<int, int>;
            if (cart == null) { cart = new Dictionary<int, int>(); session["Cart"] = cart; }
            return cart;
        }
        public static CartVM Read(StoreDbContext db, HttpSessionStateBase session)
        {
            var cart = Get(session); var ids = cart.Keys.ToList();
            var products = db.Products.AsNoTracking().Where(x => ids.Contains(x.ProductID)).ToList();
            // Removed catalog products cannot remain as invisible session entries.
            foreach (var id in ids.Except(products.Select(x => x.ProductID)).ToList()) cart.Remove(id);
            return new CartVM { Items = products.Select(p => new CartLineVM {
                ProductID=p.ProductID, ProductName=p.ProductName, ProductImage=p.ProductImage,
                Price=p.ProductPrice, Quantity=cart[p.ProductID], StockQuantity=p.IsActive ? p.StockQuantity : 0
            }).ToList() };
        }
    }
}

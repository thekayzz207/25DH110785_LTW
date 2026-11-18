using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;

namespace LTW_25DH110785.Models
{
    public class StoreDbContext : DbContext
    {
        static StoreDbContext() { Database.SetInitializer<StoreDbContext>(null); }
        public StoreDbContext() : base("name=StoreDbContext") { }
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        protected override void OnModelCreating(DbModelBuilder b)
        {
            b.Conventions.Remove<PluralizingTableNameConvention>();
            b.Entity<User>().ToTable("User").HasKey(x => x.Username);
            b.Entity<Customer>().ToTable("Customer");
            b.Entity<Category>().ToTable("Category");
            b.Entity<Product>().ToTable("Product");
            b.Entity<Order>().ToTable("Order");
            b.Entity<OrderDetail>().ToTable("OrderDetail");
            b.Entity<Customer>().HasRequired(x => x.User).WithMany().HasForeignKey(x => x.Username).WillCascadeOnDelete(false);
            b.Entity<Product>().HasRequired(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryID).WillCascadeOnDelete(false);
            b.Entity<Order>().HasRequired(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerID).WillCascadeOnDelete(false);
            b.Entity<OrderDetail>().HasRequired(x => x.Order).WithMany(x => x.OrderDetails).HasForeignKey(x => x.OrderID).WillCascadeOnDelete(false);
            b.Entity<OrderDetail>().HasRequired(x => x.Product).WithMany(x => x.OrderDetails).HasForeignKey(x => x.ProductID).WillCascadeOnDelete(false);
            b.Entity<Product>().Property(x => x.ProductPrice).HasPrecision(18, 2);
            b.Entity<Order>().Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Entity<Order>().Property(x => x.OrderDate).HasColumnType("datetime2");
            b.Entity<OrderDetail>().Property(x => x.UnitPrice).HasPrecision(18, 2);
            base.OnModelCreating(b);
        }
    }
}

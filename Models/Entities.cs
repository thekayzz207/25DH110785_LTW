using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LTW_25DH110785.Models
{
    public class User
    {
        [Key, StringLength(100)] public string Username { get; set; }
        [Required, StringLength(255)] public string Password { get; set; }
        [Required, StringLength(1)] public string UserRole { get; set; }
    }
    public class Customer
    {
        public int CustomerID { get; set; }
        [Required, StringLength(120)] public string CustomerName { get; set; }
        [Required, StringLength(15)] public string CustomerPhone { get; set; }
        [StringLength(255)] public string CustomerEmail { get; set; }
        [StringLength(500)] public string CustomerAddress { get; set; }
        [Required, StringLength(100)] public string Username { get; set; }
        public virtual User User { get; set; }
    }
    public class Category
    {
        public Category() { Products = new HashSet<Product>(); }
        public int CategoryID { get; set; }
        [Required, StringLength(100)] public string CategoryName { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }
    public class Product
    {
        public Product() { OrderDetails = new HashSet<OrderDetail>(); }
        public int ProductID { get; set; }
        public int CategoryID { get; set; }
        [Required, StringLength(200)] public string ProductName { get; set; }
        [Required] public string ProductDecription { get; set; }
        [Range(typeof(decimal), "1", "1000000000")] public decimal ProductPrice { get; set; }
        [StringLength(255)] public string ProductImage { get; set; }
        [Required, StringLength(100)] public string Brand { get; set; }
        [Required, StringLength(10)] public string DeviceType { get; set; }
        [Range(0, 100000)] public int StockQuantity { get; set; }
        public string Specifications { get; set; }
        public bool IsActive { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; }
        public virtual Category Category { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
    public class Order
    {
        public Order() { OrderDetails = new HashSet<OrderDetail>(); PaymentMethod = "COD"; }
        public int OrderID { get; set; }
        public int CustomerID { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        [Required, StringLength(20)] public string PaymentMethod { get; set; }
        [Required, StringLength(20)] public string PaymentStatus { get; set; }
        [Required, StringLength(500)] public string AddressDelivery { get; set; }
        [Required, StringLength(20)] public string OrderStatus { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
    public class OrderDetail
    {
        public int ID { get; set; }
        public int ProductID { get; set; }
        public int OrderID { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public virtual Product Product { get; set; }
        public virtual Order Order { get; set; }
    }
}

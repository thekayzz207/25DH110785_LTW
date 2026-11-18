using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using LTW_25DH110785.Models;
using LTW_25DH110785.Services;

namespace LTW_25DH110785.ViewModels
{
    public class CatalogVM
    {
        public List<Product> Products { get; set; }
        public List<Category> Categories { get; set; }
        public string Q { get; set; }
        public int? CategoryId { get; set; }
        public string Device { get; set; }
        public string Sort { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }
    public class ProductDetailVM
    {
        public Product Product { get; set; }
        public List<Product> RelatedProducts { get; set; }
    }
    public class LoginVM
    {
        [Required(ErrorMessage="Nhập tên đăng nhập."), StringLength(100), Display(Name="Tên đăng nhập")] public string Username { get; set; }
        [Required(ErrorMessage="Nhập mật khẩu."), DataType(DataType.Password), Display(Name="Mật khẩu")] public string Password { get; set; }
        public string ReturnUrl { get; set; }
    }
    public class RegisterVM
    {
        [Required, StringLength(100, MinimumLength=3), RegularExpression(@"[a-zA-Z0-9_.-]+", ErrorMessage="Chỉ dùng chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang."), Display(Name="Tên đăng nhập")] public string Username { get; set; }
        [Required, StringLength(100, MinimumLength=10), DataType(DataType.Password), Display(Name="Mật khẩu (ít nhất 10 ký tự)")] public string Password { get; set; }
        [Required, Compare("Password", ErrorMessage="Mật khẩu xác nhận chưa khớp."), DataType(DataType.Password), Display(Name="Nhập lại mật khẩu")] public string ConfirmPassword { get; set; }
        [Required, StringLength(120), Display(Name="Họ và tên")] public string CustomerName { get; set; }
        [Required, RegularExpression(@"\+?[0-9]{9,14}",ErrorMessage="Nhập số điện thoại từ 9 đến 14 chữ số."), Display(Name="Số điện thoại")] public string CustomerPhone { get; set; }
        [EmailAddress, StringLength(255), Display(Name="Email")] public string CustomerEmail { get; set; }
        [StringLength(500), Display(Name="Địa chỉ")] public string CustomerAddress { get; set; }
    }
    public class ProfileVM
    {
        [Required, StringLength(120), Display(Name="Họ và tên")] public string CustomerName { get; set; }
        [Required, RegularExpression(@"\+?[0-9]{9,14}"), Display(Name="Số điện thoại")] public string CustomerPhone { get; set; }
        [EmailAddress, StringLength(255), Display(Name="Email")] public string CustomerEmail { get; set; }
        [StringLength(500), Display(Name="Địa chỉ")] public string CustomerAddress { get; set; }
    }
    public class ChangePasswordVM
    {
        [Required, DataType(DataType.Password), Display(Name="Mật khẩu hiện tại")] public string CurrentPassword { get; set; }
        [Required, StringLength(100, MinimumLength=10), DataType(DataType.Password), Display(Name="Mật khẩu mới")] public string NewPassword { get; set; }
        [Required, Compare("NewPassword"), DataType(DataType.Password), Display(Name="Nhập lại mật khẩu mới")] public string ConfirmPassword { get; set; }
    }
    public class CartVM
    {
        public CartVM() { Items = new List<CartLineVM>(); }
        public List<CartLineVM> Items { get; set; }
        public decimal Total { get { return Items.Sum(x => x.Subtotal); } }
    }
    public class CartLineVM
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int StockQuantity { get; set; }
        public decimal Subtotal { get { return Price * Quantity; } }
    }
    public class CheckoutVM
    {
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        [Required(ErrorMessage="Nhập địa chỉ nhận hàng."), StringLength(500), Display(Name="Địa chỉ nhận hàng")] public string AddressDelivery { get; set; }
        public string CheckoutToken { get; set; }
        [Required(ErrorMessage="Chọn phương thức thanh toán."), RegularExpression(PaymentRules.MethodPattern, ErrorMessage="Phương thức thanh toán không hợp lệ.")]
        public string PaymentMethod { get; set; }
        public bool BankTransferAvailable { get; set; }
        public string BankQrImageUrl { get; set; }
        public CartVM Cart { get; set; }
    }

    public class MyOrderVM
    {
        public string Q { get; set; }
        public string SelectedStatus { get; set; }
        public bool HasAnyOrders { get; set; }
        public int MatchedCount { get; set; }
        public List<MyOrderTabVM> Tabs { get; set; } = new List<MyOrderTabVM>();
        public List<MyOrderGroupVM> Groups { get; set; } = new List<MyOrderGroupVM>();
    }
    public class MyOrderTabVM
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
    }
    public class MyOrderGroupVM
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public int TotalCount { get; set; }
        public bool IsExpanded { get; set; }
        public List<Order> Orders { get; set; } = new List<Order>();
    }
}

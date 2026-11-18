using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using LTW_25DH110785.Models;

namespace LTW_25DH110785.Areas.Admin.Models
{
    public class PagedListVM<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public string Q { get; set; }
        public string Sort { get; set; }
    }

    public class DashboardVM
    {
        public int ProductCount { get; set; }
        public int OrderCount { get; set; }
        public int CustomerCount { get; set; }
        public decimal Revenue { get; set; }
        public List<Order> RecentOrders { get; set; } = new List<Order>();
    }

    public class CategoryFormVM
    {
        public int CategoryID { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
        [StringLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự.")]
        [Display(Name = "Tên danh mục")]
        public string CategoryName { get; set; }
    }

    public class ProductFormVM
    {
        public int ProductID { get; set; }
        public string RowVersionToken { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
        [Display(Name = "Danh mục")]
        public int CategoryID { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
        [StringLength(200)]
        [Display(Name = "Tên sản phẩm")]
        public string ProductName { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập mô tả.")]
        [StringLength(4000)]
        [Display(Name = "Mô tả")]
        public string ProductDecription { get; set; }
        [Range(typeof(decimal), "1", "1000000000", ErrorMessage = "Giá bán từ 1 đến 1.000.000.000 đồng.")]
        [Display(Name = "Giá bán (đồng)")]
        public decimal ProductPrice { get; set; }
        public string ProductImage { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập thương hiệu.")]
        [StringLength(100)]
        [Display(Name = "Thương hiệu")]
        public string Brand { get; set; }
        [Required]
        [RegularExpression("^(PC|Laptop|Both)$", ErrorMessage = "Thiết bị không hợp lệ.")]
        [Display(Name = "Dùng cho")]
        public string DeviceType { get; set; } = "Both";
        [Range(0, 100000, ErrorMessage = "Tồn kho phải từ 0 đến 100.000.")]
        [Display(Name = "Số lượng tồn kho")]
        public int StockQuantity { get; set; }
        [StringLength(8000)]
        [Display(Name = "Thông số kỹ thuật")]
        public string Specifications { get; set; }
        [Display(Name = "Đang bán")]
        public bool IsActive { get; set; } = true;
        [Display(Name = "Ảnh sản phẩm")]
        public HttpPostedFileBase ImageFile { get; set; }
    }

    public class AdminUserVM
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
        [StringLength(100, MinimumLength = 3)]
        [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Chỉ dùng chữ, số, dấu chấm, gạch dưới và gạch ngang.")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; }
        [StringLength(128, MinimumLength = 10, ErrorMessage = "Mật khẩu cần từ 10 đến 128 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận chưa khớp.")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; }
    }
}

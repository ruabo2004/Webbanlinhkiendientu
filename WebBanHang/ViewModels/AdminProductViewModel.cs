using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class AdminProductViewModel
    {
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tn sản phẩm")]
        public String ProductName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tn nội dung sản phẩm")]
        [AllowHtml]
        public String Detail { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập gi sản phẩm")]
        public long Price { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập gi giám")]
        public long SalePrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng")]
        public int Stock { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhm sản phẩm")]
        public int GroupID { get; set; }

        public bool UseMultiColor { get; set; }

        public bool Active { get; set; }

        public bool IsNew { get; set; }
        public List<ProductColor> ProductColor { get; set; }
        public List<ProductAttribute> ProductAttribute { get; set; }

        public AdminProductViewModel()
        {
            ProductColor = new List<ProductColor>();
            ProductAttribute = new List<ProductAttribute>();
            IsNew = false; // Default value
        }
    }
}
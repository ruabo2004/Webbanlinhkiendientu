using System;
using System.ComponentModel.DataAnnotations;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class ShippingViewModel
    {
        [Required]
        public int CustomerID { get; set; }

        [Required]
        public String FullName { get; set; }

        [Required]
        public String Phone { get; set; }

        [Required]
        public String Address { get; set; }

        [Required]
        public int ProvinceID { get; set; }

        [Required]
        public int DistrictID { get; set; }

        [Required]
        public int WardID { get; set; }

        public Province Province { get; set; }
        public District District { get; set; }
        public Ward Ward { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public int PaymentMethod { get; set; }

        [StringLength(400, ErrorMessage = "Nội dung ghi chú thêm không được phép vuợt quá {1} ký tự")]
        public String Comment { get; set; }
    }
}
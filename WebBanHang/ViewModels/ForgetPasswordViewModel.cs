using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class ForgetPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không chính xác")]
        [Display(Name = "Email")]
        public String Email { get; set; }
    }
}


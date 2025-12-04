using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "Token không hợp lệ")]
        public String Token { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        [MaxLength(16, ErrorMessage = "Mật khẩu không quá 16 ký tự")]
        [Display(Name = "Mật khẩu mới")]
        public String NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [Display(Name = "Xác nhận mật khẩu")]
        public String ConfirmPassword { get; set; }
    }
}


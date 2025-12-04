using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class SignInViewModel
    {
        public String FacebookID { get; set; }
        public String GoogleID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không chính xác")]
        public String Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        //[MinLength(6,ErrorMessage="M?t kh?u khng du?c du?i 6 k t?")]
        [MaxLength(16, ErrorMessage = "M?t kh?u khng qu 16 k t?")]
        public String Password { get; set; }

        public bool Remember { get; set; }
    }
}
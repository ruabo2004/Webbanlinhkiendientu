using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ và tên")]
        public String FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public String Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Display(Name = "Số điện thoại")]
        [RegularExpression("^[0][0-9]{9,10}", ErrorMessage = "Số điện thoại nhập vô không hợp lệ")]
        public String Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung liên hệ")]
        [Display(Name = "Nội dung")]
        public String Message { get; set; }
    }
}
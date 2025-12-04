using System.ComponentModel.DataAnnotations;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class ProfileViewModel
    {
        public int CustomerID { get; set; }

        [Display(Name = "Mật khẩu hiện tại")]
        public string Passwrord { get; set; }

        [Display(Name = "Mật khẩu mới")]
        [StringLength(16, MinimumLength = 6, ErrorMessage = "Mật khẩu từ {2} đến {1} ký tự")]
        public string NewPasswrord { get; set; }

        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPasswrord { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên đầy đủ")]
        [StringLength(50, ErrorMessage = "Tên không được di quá {1} ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        [Display(Name = "Địa chỉ")]
        [StringLength(100, ErrorMessage = "Địa chỉ không được di quá {1} ký tự")]
        public string Address { get; set; }

        [Display(Name = "Tỉnh/Thành")]
        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành")]
        public int ProvinceID { get; set; }

        [Display(Name = "Quận/Huyện")]
        [Required(ErrorMessage = "Vui lòng chọn Quận/Huyện")]
        public int DistrictID { get; set; }

        [Display(Name = "Phường/Xã")]
        [Required(ErrorMessage = "Vui lòng chọn Phường/Xã")]
        public int WardID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Display(Name = "Số điện thoại")]
        [RegularExpression("^[0][0-9]{9,10}", ErrorMessage = "Số điện thoại nhập vô không hợp lệ")]
        public string Phone { get; set; }
    }
}
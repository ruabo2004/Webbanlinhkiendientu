using System.ComponentModel.DataAnnotations;

namespace WebBanLinhKienDienTu.ViewModels
{
    public class AdminGroupProductViewModel
    {
        public int GroupID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tn nhm")]
        public string GroupName { get; set; }

        public string ParentGroupID { get; set; }
        public string Icon { get; set; }
        public int Priority { get; set; }
    }
}
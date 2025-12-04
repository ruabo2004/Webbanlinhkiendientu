using System;
using System.Web.Mvc;

namespace WebBanLinhKienDienTu.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "Admin";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            // Kiểm tra và xóa route cũ nếu đã tồn tại để tránh lỗi duplicate
            var existingRoute = context.Routes["Admin_default"];
            if (existingRoute != null)
            {
                context.Routes.Remove(existingRoute);
            }

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
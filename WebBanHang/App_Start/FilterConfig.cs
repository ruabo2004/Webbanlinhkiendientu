using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;

namespace WebBanLinhKienDienTu
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new Utf8EncodingFilter());
        }
    }
}
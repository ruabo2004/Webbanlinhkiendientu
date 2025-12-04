using System.Web.Mvc;
using System.Web.Routing;

namespace WebBanLinhKienDienTu
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                     name: "defaultWithoutAction",
                     url: "{controller}/{id}",
                     defaults: new { action = "Index" },
                     constraints: new { id = @"\d+" },
                     namespaces: new[] { "WebBanLinhKienDienTu.Controllers" }
                   );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "WebBanLinhKienDienTu.Controllers" }
            );
        }
    }
}
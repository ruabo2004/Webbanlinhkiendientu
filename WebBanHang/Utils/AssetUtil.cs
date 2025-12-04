using System;
using System.Web;
using System.Web.Mvc;

namespace WebBanLinhKienDienTu.Utils
{
    public class AssetUtil
    {
        public static String ResourceUrl(String path)
        {
            UrlHelper url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            return url.Content("~/Content/" + path);
        }
    }
}
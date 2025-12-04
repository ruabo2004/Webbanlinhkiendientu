using System.Text;
using System.Web;
using System.Web.Mvc;

namespace WebBanLinhKienDienTu.Core
{
    /// <summary>
    /// Filter để đảm bảo response encoding UTF-8 cho tất cả các request
    /// </summary>
    public class Utf8EncodingFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var response = filterContext.HttpContext.Response;
            response.ContentEncoding = Encoding.UTF8;
            response.Charset = "utf-8";
            
            // Đảm bảo Content-Type header có charset
            if (!string.IsNullOrEmpty(response.ContentType))
            {
                if (!response.ContentType.Contains("charset"))
                {
                    response.ContentType = response.ContentType + "; charset=utf-8";
                }
            }
            
            base.OnActionExecuting(filterContext);
        }

        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var response = filterContext.HttpContext.Response;
            response.ContentEncoding = Encoding.UTF8;
            response.Charset = "utf-8";
            
            base.OnResultExecuting(filterContext);
        }
    }
}


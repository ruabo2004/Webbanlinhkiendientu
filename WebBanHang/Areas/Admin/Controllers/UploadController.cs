using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;

namespace WebBanLinhKienDienTu.Areas.Admin.Controllers
{
    [Security]
    public class UploadController : AdminBaseController
    {
        //
        // GET: /Admin/Upload/
        public ActionResult Index()
        {
            return View();
        }
    }
}
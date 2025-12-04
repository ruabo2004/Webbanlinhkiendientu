using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Core.RepositoryModel;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Controllers
{
    public class WidgetController : BaseController
    {
        //
        // GET: /Widget/
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Navbar()
        {
            var menus = Repository.Bind<MenuRepository>().FetchAll().OrderByDescending(item => item.Priority);
            return PartialView(menus);
        }

        public ActionResult BestSellingProduct()
        {
            return View();
        }

        public ActionResult LatestProduct()
        {
            var list = Repository.Product.GetNewProduct(9);
            return PartialView(list);
        }

        public ActionResult Search()
        {
            var model = Repository.GroupProduct.GetTopGroupProducts();
            // Đảm bảo luôn trả về một list, không bao giờ null
            if (model == null)
            {
                model = new List<GroupProduct>();
            }
            return PartialView(model);
        }
    }
}
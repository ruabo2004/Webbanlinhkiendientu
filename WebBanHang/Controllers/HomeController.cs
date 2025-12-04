using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Dynamic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Core.RepositoryModel;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            var productRes = Repository.Bind<ProductRepository>();
            var groupRes = Repository.Bind<GroupProductRepository>();
            dynamic model = new ExpandoObject();
            model.NewProduct = productRes.GetNewProduct(5);
            model.GroupProducts = groupRes.GetTopGroupProducts();
            return View(model);
        }

        // Child action - không cache để luôn load fresh data từ CSDL
        public ActionResult SideBarMenu()
        {
            var db = Repository.DbContext;
            // Eager load đầy đủ các cấp: parent -> child -> grandchild
            // Sử dụng AsNoTracking() để đảm bảo load fresh data từ CSDL, không dùng cache của EF
            var groupProduct = db.Set<GroupProduct>()
                            .AsNoTracking()
                            .Where(item => item.ParentGroupID == null)
                            .Include(item => item.GroupProducts1.Select(child => child.GroupProducts1))
                            .OrderByDescending(item => item.Priority)
                            .ToList();
            // Đảm bảo luôn trả về một list, không bao giờ null
            if (groupProduct == null)
            {
                groupProduct = new List<GroupProduct>();
            }
            return PartialView(groupProduct);
        }

        public ActionResult ShowGroupItem(int id)
        {
            var groupRes = Repository.Bind<GroupProductRepository>();
            dynamic model = new ExpandoObject();
            List<Product> products = groupRes.GetProductInGroups(id);
            model.Products = products;
            model.Group = groupRes.FindById(id);
            if (products.Count == 0) return Content("");
            return PartialView(model);
        }

        /// <summary>
        /// API để lấy CustomerID hiện tại (dùng cho chat widget)
        /// </summary>
        [HttpPost]
        public JsonResult GetCurrentCustomerId()
        {
            var customer = UserManager.CurrentCustomer;
            return Json(new { customerId = customer != null ? customer.CustomerID : 0 }, JsonRequestBehavior.AllowGet);
        }
    }
}
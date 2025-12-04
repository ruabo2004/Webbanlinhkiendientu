using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Core.RepositoryModel;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Utils;

namespace WebBanLinhKienDienTu.Controllers
{
    public class CategoryController : BaseController
    {
        private GroupProductRepository groupRepository;

        public CategoryController()
        {
            groupRepository = Repository.Bind<GroupProductRepository>();
        }

        //
        // GET: /Category/
        public ActionResult Index(int? id)
        {
            return Detail(id);
        }

        public ActionResult Detail(int? id)
        {
            if (id == null)
                return RedirectToAction("NotFound", "Error");
            var model = Repository.GroupProduct.FindById(id);
            if (model == null) return RedirectToAction("Error404", "Error");
            return View("Detail", model);
        }

        public ActionResult ListGroupProduct(int id)
        {
            dynamic model = new ExpandoObject();
            model.GroupProducts = groupRepository.GetListSubGroups(id);
            model.CurrentGroup = groupRepository.FindById(id);
            return PartialView(model);
        }

        public ActionResult ShowProductInCategory(int id, string range_price = null, string sort = null)
        {
            // Tạo NameValueCollection từ các tham số được truyền vào
            var filter = new System.Collections.Specialized.NameValueCollection();
            if (!string.IsNullOrEmpty(range_price))
            {
                filter["range_price"] = range_price;
            }
            if (!string.IsNullOrEmpty(sort))
            {
                filter["sort"] = sort;
            }
            // Nếu không có tham số từ route, thử lấy từ Request.QueryString (cho trường hợp gọi trực tiếp)
            if (filter.Count == 0 && Request.QueryString.Count > 0)
            {
                filter = Request.QueryString;
            }

            var model = groupRepository.GetProductInGroups(id, filter);
            var productList = model != null ? model.ToList() : new List<Product>();

            ViewData["groupID"] = id;
            return PartialView(productList);
        }
    }
}
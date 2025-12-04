using System;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Core.RepositoryModel;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.ViewModels;

namespace WebBanLinhKienDienTu.Controllers
{
    public class ProductManagerController : BaseController
    {
        //
        // GET: /ProductManager/
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Add()
        {
            var groupRepo = Repository.Bind<GroupProductRepository>();
            ViewBag.GroupProducts = groupRepo.FetchAll();
            return View();
        }

        [HttpPost]
        public ActionResult Add(FormCollection form, ProductViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                var groupRepo = Repository.Bind<GroupProductRepository>();
                ViewBag.GroupProducts = groupRepo.FetchAll();
                return View(viewModel);
            }
            Product product = new Product
            {
                ProductName = viewModel.ProductName,
                Detail = viewModel.Detail,
                GroupID = viewModel.GroupID,
                Price = viewModel.Price,
                SalePrice = viewModel.SalePrice,
                Stock = viewModel.Stock,
                Active = viewModel.Active,
                CreateDate = DateTime.Now
            };
            var productRepo = Repository.Bind<ProductRepository>();
            Product insert = productRepo.Insert(product);
            productRepo.SaveChanges();

            return RedirectToAction("Add");
        }
    }
}
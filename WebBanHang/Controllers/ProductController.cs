using System;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Core.RepositoryModel;

namespace WebBanLinhKienDienTu.Controllers
{
    public class ProductController : BaseController
    {
        //
        // GET: /Product/
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Detail(int id)
        {
            var productRepository = Repository.Bind<ProductRepository>();
            var model = productRepository.FindById(id);
            ViewBag.Sale = Repository.Product.BestProductSale();

            return View(model);
        }

        public ActionResult Search(int? group, String q)
        {
            var products = Repository.Product.FetchAll().Where(p => p.Active); // Chỉ lấy sản phẩm active
            if (group != null && group != 0)
            {
                products = products.Where(p => p.GroupID == group);
            }
            if (!String.IsNullOrEmpty(q))
            {
                var searchTerm = q.ToLower().Trim();
                products = products.Where(p => p.ProductName.ToLower().Contains(searchTerm));
            }
            ViewBag.Query = q ?? "";
            return View(products.ToList()); // Materialize để đảm bảo có thể đếm được
        }

        [HttpPost]
        [Authorize]
        public ActionResult AddComment(int productId, byte rate, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập nội dung đánh giá!";
                    return RedirectToAction("Detail", new { id = productId });
                }

                // Get current customer
                var customer = Repository.Customer.FetchAll()
                    .FirstOrDefault(c => c.Email == User.Identity.Name);

                var comment = new Models.Comment
                {
                    ProductID = productId,
                    CommentContent = content.Trim(),
                    CommentTime = DateTime.Now,
                    CustomerID = customer?.CustomerID,
                    Rate = rate
                };

                var commentRepo = Repository.Create<Models.Comment>();
                commentRepo.Insert(comment);
                commentRepo.SaveChanges();

                TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá sản phẩm!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi gửi đánh giá. Vui lòng thử lại!";
            }

            return RedirectToAction("Detail", new { id = productId });
        }
    }
}

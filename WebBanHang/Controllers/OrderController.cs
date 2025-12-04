using System;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;

namespace WebBanLinhKienDienTu.Controllers
{
    [Authorize]
    public class OrderController : BaseController
    {
        //
        // GET: /Order/
        public ActionResult Index()
        {
            var customer = UserManager.CurrentCustomer;
            if (customer == null)
            {
                return RedirectToAction("Login", "Customer");
            }

            // Lấy tất cả đơn hàng của customer với eager loading, sắp xếp mới nhất trước
            var orders = Repository.Order.FetchAll()
                .Where(o => o.CustomerID == customer.CustomerID)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            // Đảm bảo load navigation properties
            foreach (var order in orders)
            {
                var orderDetails = order.OrderDetails.ToList();
                var orderStatus = order.OrderStatu;
                var payment = order.Payment;
                foreach (var detail in orderDetails)
                {
                    var product = detail.Product;
                }
            }

            return View(orders);
        }

        //
        // GET: /Order/Detail/5
        public ActionResult Detail(int id)
        {
            var customer = UserManager.CurrentCustomer;
            if (customer == null)
            {
                return RedirectToAction("Login", "Customer");
            }

            // Lấy đơn hàng và kiểm tra quyền sở hữu
            var order = Repository.Order.FetchAll()
                .Where(o => o.OrderID == id && o.CustomerID == customer.CustomerID)
                .FirstOrDefault();

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.";
                return RedirectToAction("Index");
            }

            // Eager load navigation properties
            var orderDetails = order.OrderDetails.ToList();
            var orderStatus = order.OrderStatu;
            var payment = order.Payment;
            var province = order.Province;
            var district = order.District;
            var ward = order.Ward;
            
            foreach (var detail in orderDetails)
            {
                var product = detail.Product;
                var color = detail.Color;
                if (product != null && product.ImageProducts != null)
                {
                    var images = product.ImageProducts.ToList();
                }
            }

            return View(order);
        }

        //
        // POST: /Order/Cancel/5
        [HttpPost]
        public JsonResult Cancel(int id)
        {
            try
            {
                var customer = UserManager.CurrentCustomer;
                if (customer == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });
                }

                var order = Repository.Order.FetchAll()
                    .Where(o => o.OrderID == id && o.CustomerID == customer.CustomerID)
                    .FirstOrDefault();

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                // Chỉ cho phép hủy đơn hàng đang chờ xử lý (OrderStatusID = 1)
                if (order.OrderStatusID != 1)
                {
                    return Json(new { success = false, message = "Không thể hủy đơn hàng này. Đơn hàng đã được xử lý hoặc đã hoàn thành." });
                }

                // Cập nhật trạng thái thành "Đã hủy" (OrderStatusID = 4)
                order.OrderStatusID = 4;
                Repository.SaveChanges();

                return Json(new { success = true, message = "Đã hủy đơn hàng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}


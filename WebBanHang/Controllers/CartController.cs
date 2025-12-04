using Newtonsoft.Json;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Utils;

namespace WebBanLinhKienDienTu.Controllers
{
    public class CartController : BaseController
    {
        //
        // GET: /Cart/
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult CartTotal()
        {
            return PartialView(Cart);
        }

        [HttpPost]
        public ActionResult AddCart(int? id, int? color, int quantity)
        {
            if (id == null) return HttpNotFound("Id bị trống");
            Product product = Repository.Product.FindById(id);
            Color colorItem = Repository.Color.FindById(color);
            if (product == null) return HttpNotFound("Không tồn tại sản phẩm");
            Cart.AddItem(product, colorItem, quantity);
            return PartialView("ShoppingCartView");
        }

        [HttpPost]
        public ActionResult RemoveCart(int? id, int? color)
        {
            if (id == null) return HttpNotFound("Id bị trống");
            Product product = Repository.Product.FindById(id);
            Color colorItem = Repository.Color.FindById(color);
            if (product == null) return HttpNotFound("Không tồn tại sản phẩm");
            Cart.Remove(product, colorItem);
            return PartialView("ShoppingCartView");
        }

        [HttpPost]
        public ActionResult UpdateCart(int? id, int? color, int quantity)
        {
            if (id == null) return HttpNotFound("Id empty");
            Product product = Repository.Product.FindById(id);
            Color colorItem = Repository.Color.FindById(color);
            if (product == null) return HttpNotFound("Item not found");
            var item = Cart.Update(product, colorItem, quantity);
            if (item != null)
                return Content(HtmlExtension.FormatCurrency(item.TotalPrice) + " đ");
            return HttpNotFound();
        }

        public ActionResult GetListColor(int? id)
        {
            dynamic result = new ExpandoObject();
            var colors = new List<object>();
            result.status = "";
            result.message = "";
            result.count = colors.Count;
            result.colors = colors;

            if (id == null)
            {
                result.status = "error";
                result.message = "ID bị trống";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            Product product = Repository.Product.FindById(id);
            if (product == null)
            {
                result.status = "error";
                result.message = "Sản phẩm không tồn tại";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }

            result.status = "OK";
            result.count = product.ProductColors.Count;
            foreach (var color in product.ProductColors)
            {
                colors.Add(new
                {
                    color_id = color.ColorID,
                    color_name = color.Color.ColorName,
                    hex_code = color.Color.HexCode
                });
            }
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public ActionResult ShoppingCartView()
        {
            return PartialView();
        }

        public ActionResult CartHeaderView()
        {
            return PartialView();
        }

        [HttpGet]
        public JsonResult GetCartCount()
        {
            return Json(new { count = Cart.GetCount() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult ApplyCoupon(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Json(new { success = false, message = "Vui lòng nhập mã giảm giá!" });
                }

                if (Cart == null || Cart.Items == null || Cart.Items.Count == 0)
                {
                    return Json(new { success = false, message = "Giỏ hàng trống!" });
                }

            code = code.Trim().ToUpper();

            // Kiểm tra coupon có tồn tại không
            var coupon = Repository.Coupon.FetchAll()
                .Where(c => c.Code == code && c.Active)
                .FirstOrDefault();
            if (coupon == null)
            {
                return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã hết hạn!" });
            }

                // Kiểm tra thời hạn
                var now = System.DateTime.Now;
                if (!coupon.Indefinite.GetValueOrDefault() && now < coupon.StartDate)
                {
                    return Json(new { success = false, message = "Mã giảm giá chưa đến thời gian áp dụng!" });
                }
                if (!coupon.Indefinite.GetValueOrDefault() && now > coupon.EndDate)
                {
                    return Json(new { success = false, message = "Mã giảm giá đã hết hạn!" });
                }

                // Lấy danh sách GroupID bị loại trừ
                var excludedGroupIds = Repository.ExcludeCoupon.FetchAll()
                    .Where(e => e.CouponID == coupon.CouponID)
                    .Select(e => e.GroupID)
                    .ToList();

                // Tính subtotal từ các sản phẩm không bị loại trừ
                long eligibleSubtotal = 0;
                foreach (var item in Cart.Items)
                {
                    // Kiểm tra sản phẩm có bị loại trừ không
                    if (item.Product != null && !excludedGroupIds.Contains(item.Product.GroupID))
                    {
                        eligibleSubtotal += item.TotalPrice;
                    }
                }

                if (eligibleSubtotal == 0)
                {
                    return Json(new { success = false, message = "Mã giảm giá không áp dụng cho các sản phẩm trong giỏ hàng!" });
                }

                // Tính discount
                long discount = 0;
                if (coupon.Type == "Percent")
                {
                    discount = (long)(eligibleSubtotal * coupon.Discount.GetValueOrDefault() / 100);
                }
                else // Amount
                {
                    discount = (long)coupon.Discount.GetValueOrDefault();
                }

                // Lưu vào Session
                Session["CouponCode"] = coupon.Code;
                Session["CouponDiscount"] = discount;
                Session["CouponFreeShip"] = coupon.FreeShip;

                return Json(new { success = true, message = "Áp dụng mã giảm giá thành công!" });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult RemoveCoupon()
        {
            Session.Remove("CouponCode");
            Session.Remove("CouponDiscount");
            Session.Remove("CouponFreeShip");
            return Json(new { success = true, message = "Đã xóa mã giảm giá!" });
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Areas.Admin.Controllers
{
    [Security]
    public class CouponController : AdminBaseController
    {
        // GET: Admin/Coupon
        public ActionResult Index()
        {
            return View();
        }

        // GET: Admin/Coupon/Create
        public ActionResult Create()
        {
            var model = new Coupon
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(1),
                Active = true,
                FreeShip = false,
                Indefinite = false
            };
            return View(model);
        }

        // POST: Admin/Coupon/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Coupon model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra code đã tồn tại chưa
                    var existingCoupon = Repository.Coupon.FetchAll()
                        .Where(c => c.Code == model.Code)
                        .FirstOrDefault();
                    
                    if (existingCoupon != null)
                    {
                        ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại!");
                        return View(model);
                    }

                    Repository.Coupon.Insert(model);
                    Repository.Coupon.SaveChanges();
                    
                    TempData["Success"] = "Tạo mã giảm giá thành công!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                }
            }
            return View(model);
        }

        // GET: Admin/Coupon/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            var coupon = Repository.Coupon.FindById(id);
            if (coupon == null)
            {
                return HttpNotFound();
            }

            return View(coupon);
        }

        // POST: Admin/Coupon/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Coupon model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Repository.Coupon.Update(model);
                    Repository.Coupon.SaveChanges();
                    
                    TempData["Success"] = "Cập nhật mã giảm giá thành công!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                }
            }
            return View(model);
        }

        // AJAX: Load Coupons for DataTables
        public ActionResult LoadCoupons(int start, int length)
        {
            var search = Request.QueryString["search[value]"]?.ToString() ?? "";
            
            var coupons = Repository.Coupon.FetchAll()
                .Where(c => string.IsNullOrEmpty(search) || c.Code.Contains(search) || c.Type.Contains(search));
            
            coupons = coupons.OrderByDescending(c => c.CouponID);
            var couponPaging = coupons.Skip(start).Take(length).ToList();
            
            List<object> data = new List<object>();
            foreach (var coupon in couponPaging)
            {
                var row = new List<object>();
                row.Add(coupon.CouponID.ToString());
                row.Add(coupon.Code);
                row.Add(coupon.Discount.ToString());
                row.Add(coupon.Type);
                row.Add(coupon.FreeShip.HasValue && coupon.FreeShip.Value ? "Có" : "Không");
                row.Add(coupon.StartDate.ToString("dd/MM/yyyy"));
                row.Add(coupon.EndDate.ToString("dd/MM/yyyy"));
                row.Add(coupon.Active ? "Hoạt động" : "Không hoạt động");
                data.Add(row);
            }
            
            var result = new
            {
                draw = Request.QueryString["draw"],
                recordsTotal = coupons.Count(),
                recordsFiltered = coupons.Count(),
                data = data
            };
            
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        // POST: Admin/Coupon/Delete/5
        [HttpPost]
        public ActionResult Delete(int? id)
        {
            dynamic result = new ExpandoObject();
            try
            {
                if (id == null)
                {
                    result.status = "error";
                    result.message = "ID không hợp lệ!";
                    return Content(JsonConvert.SerializeObject(result), "application/json");
                }

                var coupon = Repository.Coupon.FindById(id);
                if (coupon == null)
                {
                    result.status = "error";
                    result.message = "Không tìm thấy mã giảm giá!";
                    return Content(JsonConvert.SerializeObject(result), "application/json");
                }

                Repository.Coupon.Delete(id);
                Repository.Coupon.SaveChanges();

                result.status = "success";
                result.message = "Xóa mã giảm giá thành công!";
            }
            catch (Exception ex)
            {
                result.status = "error";
                result.message = "Có lỗi xảy ra: " + ex.Message;
            }

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        // POST: Admin/Coupon/ToggleActive/5
        [HttpPost]
        public ActionResult ToggleActive(int? id)
        {
            dynamic result = new ExpandoObject();
            try
            {
                if (id == null)
                {
                    result.status = "error";
                    result.message = "ID không hợp lệ!";
                    return Content(JsonConvert.SerializeObject(result), "application/json");
                }

                var coupon = Repository.Coupon.FindById(id);
                if (coupon == null)
                {
                    result.status = "error";
                    result.message = "Không tìm thấy mã giảm giá!";
                    return Content(JsonConvert.SerializeObject(result), "application/json");
                }

                coupon.Active = !coupon.Active;
                Repository.Coupon.Update(coupon);
                Repository.Coupon.SaveChanges();

                result.status = "success";
                result.message = coupon.Active ? "Đã kích hoạt mã giảm giá!" : "Đã vô hiệu hóa mã giảm giá!";
                result.active = coupon.Active;
            }
            catch (Exception ex)
            {
                result.status = "error";
                result.message = "Có lỗi xảy ra: " + ex.Message;
            }

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }
    }
}


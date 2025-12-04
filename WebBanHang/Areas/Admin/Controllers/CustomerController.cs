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
    public class CustomerController : AdminBaseController
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Load(int start, int length)
        {
            var search = Request["search[value]"];
            var customers = Repository.Customer.FetchAll().OrderByDescending(c => c.RegistrationDate).AsQueryable();

            if (!String.IsNullOrEmpty(search))
            {
                customers = customers.Where(c =>
                    c.FullName.ToLower().Contains(search)
                    || c.Email.ToLower().Contains(search)
                );
            }

            int record_count = customers.Count();
            customers = customers.Skip(start).Take(length);
            List<object> data = new List<object>();
            foreach (var customer in customers)
            {
                var latest_order = customer.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefault();
                var totalPay = customer.Orders.Where(o => o.Paid).Sum(o => (long?)o.TotalPrice) ?? 0;
                
                data.Add(new
                {
                    customer_id = customer.CustomerID,
                    customer_name = customer.FullName,
                    customer_email = customer.Email,
                    order_num = customer.Orders.Count,
                    order_latest = (latest_order != null) ? latest_order.OrderID : 0,
                    total_pay = totalPay
                });
            }

            return Content(JsonConvert.SerializeObject(new
            {
                draw = Request["draw"],
                data = data,
                recordsFiltered = record_count,
                recordsTotal = record_count
            }), "application/json");
        }

        public ActionResult Detail(int? id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }
            var customer = Repository.Customer.FindById(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            ViewBag.Provinces = Repository.Province.FetchAll().OrderBy(p => p.Type);
            return View(customer);
        }

        [HttpPost]
        public ActionResult Edit(Customer customer)
        {
            dynamic result = new ExpandoObject();
            result.success = false;
            result.message = "";
            if (customer.CustomerID == 0)
            {
                result.message = "Chua có mã khách hàng";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }

            var oldCustomer = Repository.Customer.FindById(customer.CustomerID);
            if (oldCustomer == null)
            {
                result.message = "Khách hàng không t?n t?i";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            oldCustomer.FullName = customer.FullName;
            oldCustomer.Phone = customer.Phone;
            oldCustomer.Address = customer.Address;
            oldCustomer.ProvinceID = customer.ProvinceID;
            oldCustomer.DistrictID = customer.DistrictID;
            oldCustomer.WardID = customer.WardID;
            Repository.Customer.SaveChanges();
            result.success = true;
            result.message = "C?p nh?t thành công!!!";
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public ActionResult Delete(int? id)
        {
            dynamic result = new ExpandoObject();
            result.success = false;
            result.message = "";
            
            if (id == null)
            {
                result.message = "Thi?u mã khách hàng";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }

            var customer = Repository.Customer.FindById(id);
            if (customer == null)
            {
                result.message = "Khách hàng này không t?n t?i trong h? th?ng";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }

            var pendingOrders = customer.Orders.Where(o => o.OrderStatusID != 3).ToList();
            if (pendingOrders.Count > 0)
            {
                var statusText = string.Join(", ", pendingOrders.Select(o => 
                    $"#{o.OrderID} ({(o.OrderStatusID == 1 ? "Ch? x? lý" : "Ðang x? lý")})"
                ));
                result.message = $"Không th? xóa khách hàng này vì còn {pendingOrders.Count} don hàng chua hoàn thành: {statusText}. Vui lòng hoàn thành ho?c h?y các don hàng này tru?c.";
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }

            try
            {
                var carts = customer.Carts.ToList();
                foreach (var cart in carts)
                {
                    var cartDetails = cart.CartDetails.ToList();
                    foreach (var detail in cartDetails)
                    {
                        Repository.Create<CartDetail>().Delete(detail.CartID, detail.ProductID, detail.ColorID);
                    }
                    Repository.Create<Cart>().Delete(cart.CartID);
                }

                var comments = customer.Comments.ToList();
                foreach (var comment in comments)
                {
                    Repository.Create<Comment>().Delete(comment.CommentID);
                }

                var contacts = customer.Contacts.ToList();
                foreach (var contact in contacts)
                {
                    Repository.Create<Contact>().Delete(contact.ContactID);
                }

                var completedOrders = customer.Orders.Where(o => o.OrderStatusID == 3).ToList();
                foreach (var order in completedOrders)
                {
                    var orderDetails = order.OrderDetails.ToList();
                    foreach (var detail in orderDetails)
                    {
                        Repository.OrderDetail.Delete(detail.DetailID);
                    }
                    Repository.Order.Delete(order.OrderID);
                }

                Repository.Customer.Delete(id);
                Repository.Customer.SaveChanges();

                result.success = true;
                result.message = "Xóa khách hàng kh?i h? th?ng thành công";
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = "Có l?i x?y ra khi xóa: " + ex.Message;
            }

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }
    }
}
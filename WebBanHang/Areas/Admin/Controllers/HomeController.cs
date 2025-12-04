using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Areas.Admin.Controllers
{
    [Security]
    public class HomeController : AdminBaseController
    {
        public ActionResult Index()
        {
            try
            {
                var allOrders = Repository.Order.FetchAll()?.ToList() ?? new List<Order>();
                var allProducts = Repository.Product.FetchAll()?.ToList() ?? new List<Product>();
                var allCustomers = Repository.Customer.FetchAll()?.ToList() ?? new List<Customer>();

                ViewBag.Orders = allOrders.OrderByDescending(o => o.OrderDate).Take(10).ToList();
                ViewBag.Products = allProducts;
                ViewBag.Customers = allCustomers;

                // Dữ liệu cho biểu đồ doanh thu 7 ngày gần nhất
                var revenueData = new List<long>();
                var orderCountData = new List<int>();
                var labels = new List<string>();

                for (int i = 6; i >= 0; i--)
                {
                    var date = DateTime.Now.AddDays(-i);
                    var dayOrders = allOrders.Where(o => o != null && o.OrderDate.Date == date.Date).ToList();
                    var revenue = dayOrders.Where(o => o.Paid).Sum(o => (long?)o.TotalPrice) ?? 0;
                    var orderCount = dayOrders.Count();

                    labels.Add(date.ToString("dd/MM"));
                    revenueData.Add(revenue);
                    orderCountData.Add(orderCount);
                }

                ViewBag.RevenueLabels = labels;
                ViewBag.RevenueData = revenueData;
                ViewBag.OrderCountData = orderCountData;

                // Dữ liệu cho biểu đồ phân bổ trạng thái đơn hàng
                var orderStatusData = allOrders
                    .Where(o => o != null && o.OrderStatu != null)
                    .GroupBy(o => o.OrderStatu.OrderStatusName)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToList();

                ViewBag.OrderStatusLabels = orderStatusData.Any() 
                    ? orderStatusData.Select(x => x.Status).ToList() 
                    : new List<string> { "Không có dữ liệu" };
                ViewBag.OrderStatusData = orderStatusData.Any() 
                    ? orderStatusData.Select(x => x.Count).ToList() 
                    : new List<int> { 0 };

                // Dữ liệu cho biểu đồ top sản phẩm bán chạy (10 sản phẩm)
                var orderDetailsList = allOrders
                    .Where(o => o != null && o.OrderDetails != null)
                    .SelectMany(o => o.OrderDetails)
                    .Where(od => od != null && od.Product != null)
                    .ToList();

                var topProducts = orderDetailsList
                    .GroupBy(od => new { od.ProductID, ProductName = od.Product.ProductName })
                    .Select(g => new { 
                        ProductName = g.Key.ProductName, 
                        TotalSold = g.Sum(od => (int)od.Quantity),
                        Revenue = g.Sum(od => od.Total)
                    })
                    .OrderByDescending(x => x.TotalSold)
                    .Take(10)
                    .ToList();

                ViewBag.TopProductLabels = topProducts.Any() 
                    ? topProducts.Select(x => x.ProductName.Length > 20 ? x.ProductName.Substring(0, 20) + "..." : x.ProductName).ToList()
                    : new List<string> { "Chưa có dữ liệu" };
                ViewBag.TopProductData = topProducts.Any() 
                    ? topProducts.Select(x => x.TotalSold).ToList()
                    : new List<int> { 0 };

                // Dữ liệu cho biểu đồ doanh thu theo tháng (6 tháng gần nhất)
                var monthlyRevenueLabels = new List<string>();
                var monthlyRevenueData = new List<long>();

                for (int i = 5; i >= 0; i--)
                {
                    var date = DateTime.Now.AddMonths(-i);
                    var monthOrders = allOrders.Where(o => o != null && o.OrderDate.Year == date.Year && o.OrderDate.Month == date.Month);
                    var revenue = monthOrders.Where(o => o.Paid).Sum(o => (long?)o.TotalPrice) ?? 0;

                    monthlyRevenueLabels.Add(date.ToString("MM/yyyy"));
                    monthlyRevenueData.Add(revenue);
                }

                ViewBag.MonthlyRevenueLabels = monthlyRevenueLabels;
                ViewBag.MonthlyRevenueData = monthlyRevenueData;
            }
            catch (Exception ex)
            {
                // Log error và trả về view với dữ liệu rỗng
                ViewBag.Orders = new List<Order>();
                ViewBag.Products = new List<Product>();
                ViewBag.Customers = new List<Customer>();
                ViewBag.RevenueLabels = new List<string> { "Chưa có dữ liệu" };
                ViewBag.RevenueData = new List<long> { 0 };
                ViewBag.OrderCountData = new List<int> { 0 };
                ViewBag.OrderStatusLabels = new List<string> { "Chưa có dữ liệu" };
                ViewBag.OrderStatusData = new List<int> { 0 };
                ViewBag.TopProductLabels = new List<string> { "Chưa có dữ liệu" };
                ViewBag.TopProductData = new List<int> { 0 };
                ViewBag.MonthlyRevenueLabels = new List<string> { "Chưa có dữ liệu" };
                ViewBag.MonthlyRevenueData = new List<long> { 0 };
            }

            return View("Index");
        }
    }
}
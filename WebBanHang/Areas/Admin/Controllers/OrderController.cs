using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Utils;

namespace WebBanLinhKienDienTu.Areas.Admin.Controllers
{
  [Security]
  public class OrderController : AdminBaseController
  {
    //
    // GET: /Admin/Order/
    public ActionResult Index()
    {
      return View();
    }

    public ActionResult LoadOrder(int start, int length)
    {
      var orders = Repository.Order.FetchAll();
      var search = Request.QueryString["search[value]"].ToString();
      int orderIdSearch = 0;
      Int32.TryParse(search, out orderIdSearch);

      var ordersFilter = orders
        .OrderByDescending(o => o.OrderDate)
        .AsQueryable();
      if (!String.IsNullOrEmpty(search))
      {
        ordersFilter = ordersFilter.Where(o => o.OrderID == orderIdSearch);
      }

      if (ordersFilter.Count() > 0)
        ordersFilter = ordersFilter.Skip(start).Take(length);
      List<object> data = new List<object>();
      foreach (var order in ordersFilter)
      {
        List<object> row = new List<object>();
        row.Add(order.OrderID);

        //Trạng thái đơn đặt hàng
        var statusColor = "";
        switch (order.OrderStatusID)
        {
          case 1:
            statusColor = "warning";
            break;

          case 2:
            statusColor = "info";
            break;

          case 3:
            statusColor = "success";
            break;

          default:
            statusColor = "danger";
            break;
        }

        row.Add("<span data-pk='" + order.OrderID + "' data-value='" + order.OrderStatusID + "' class='label label-" +
                statusColor + " status-order'>" + order.OrderStatu.OrderStatusName + "</span>");

        //Trạng thái thanh toán
        string text;
        if (order.Paid)
        {
          statusColor = "success";
          text = "Đã thanh toán";
        }
        else
        {
          statusColor = "warning";
          text = "Chưa thanh toán";
        }

        row.Add("<span data-pk='" + order.OrderID + "' data-value='" + (order.Paid ? 1 : 0) + "' class='label label-" +
                statusColor + " status-payment'>" + text + "</span>");

        //Trạng thái giao hàng
        switch (order.ShippingStatusID)
        {
          case 1:
            statusColor = "danger";
            break;

          case 2:
            statusColor = "info";
            break;

          case 3:
            statusColor = "success";
            break;
        }

        row.Add("<span data-pk='" + order.OrderID + "' data-value='" + order.ShippingStatusID +
                "' class='label label-" + statusColor + " status-shipping'>" + order.ShippingStatu.ShippingName +
                "</span>");

        //Thông tin khách hàng

        row.Add(order.Customer.FullName + "<br>(" + order.Customer.Email + ")");
        row.Add(order.Payment.PaymentName);
        row.Add(order.OrderDate.ToString("dd/M/yyyy hh:mm tt"));

        data.Add(row);
      }

      return Content(JsonConvert.SerializeObject(new
      {
        draw = Request.QueryString["draw"],
        recordsTotal = ordersFilter.Count(),
        recordsFiltered = ordersFilter.Count(),
        data = data
      }), "application/json");
    }

    public ActionResult LoadOrderProduct(int? id)
    {
      List<object> data = new List<object>();
      if (id == null || id == 0)
        return Content(JsonConvert.SerializeObject(new
        {
          draw = Request.QueryString["draw"],
          recordsTotal = 0,
          recordsFiltered = 0,
          sum_total = 0,
          data = data
        }), "application/json");

      // Force eager loading of OrderDetails, Product, ImageProducts, and Color
      var order = Repository.Order.FetchAll()
        .Where(o => o.OrderID == id)
        .FirstOrDefault();
      if (order == null)
      {
        return Content(JsonConvert.SerializeObject(new
        {
          draw = Request.QueryString["draw"],
          recordsTotal = 0,
          recordsFiltered = 0,
          sum_total = 0,
          data = data
        }), "application/json");
      }

      foreach (var oProduct in order.OrderDetails)
      {
        // Get image URL with full path
        var imageUrl = ImageHelper.DefaultImage();
        if (oProduct.Product.ImageProducts.Count > 0)
        {
          imageUrl = ImageHelper.ImageUrl(oProduct.Product.ImageProducts.FirstOrDefault().ImagePath);
        }

        data.Add(new
        {
          detail_id = oProduct.DetailID,
          image_url = imageUrl,
          product_name = oProduct.Product.ProductName,
          color = (oProduct.ColorID != null)
            ? "<span style=\"background-color: #" + oProduct.Color.HexCode + "\" class=\"label\">" +
              oProduct.Color.ColorName + "</span>"
            : "",
          price = oProduct.Price,
          quantity = oProduct.Quantity,
          total = oProduct.Total
        });
      }

      return Content(JsonConvert.SerializeObject(new
      {
        draw = Request.QueryString["draw"],
        recordsTotal = data.Count,
        recordsFiltered = data.Count,
        data = data,
        sum_total = order.TotalPrice
      }), "application/json");
    }

    public ActionResult Detail(int? id)
    {
      if (id == null) return new HttpNotFoundResult("ID not found");
      var order = Repository.Order.FindById(id);
      if (order == null) return new HttpNotFoundResult("Order with id " + id + " does not exist in system");
      return View(order);
    }

    /// <summary>
    /// Action để thủ công trừ số lượng sản phẩm từ kho cho đơn hàng đã hoàn thành
    /// </summary>
    [HttpPost]
    public ActionResult DeductInventory(int? id)
    {
      dynamic result = new ExpandoObject();
      result.success = false;
      result.message = "";

      if (id == null)
      {
        result.message = "Thiếu mã đơn đặt hàng";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      var order = Repository.Order.FindById(id);
      if (order == null)
      {
        result.message = "Đơn đặt hàng này không tồn tại";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      // Kiểm tra xem order có đủ 3 điều kiện chưa
      bool isComplete = (order.OrderStatusID == 3 && order.Paid && order.ShippingStatusID == 3);
      if (!isComplete)
      {
        result.message = "Đơn hàng chưa hoàn thành đủ 3 điều kiện (Trạng thái đơn, Thanh toán, Giao hàng)";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      // Kiểm tra xem đã trừ kho chưa
      if (IsInventoryDeducted(order))
      {
        result.message = "Đơn hàng này đã được trừ số lượng sản phẩm từ kho trước đó";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      try
      {
        DeductProductInventory(order);
        
        // Lưu flag đã trừ vào Comment
        MarkInventoryDeducted(order);
        Repository.Order.SaveChanges();
        
        result.success = true;
        result.message = "Đã trừ số lượng sản phẩm từ kho thành công";
      }
      catch (Exception ex)
      {
        result.message = "Có lỗi xảy ra khi trừ số lượng: " + ex.Message;
      }

      return Content(JsonConvert.SerializeObject(result), "application/json");
    }

    public ActionResult OrderStatusOption()
    {
      var orderStatus = Repository.Create<OrderStatu>().FetchAll();
      var listStatus = new List<object>();
      foreach (var status in orderStatus)
      {
        listStatus.Add(new
        {
          value = status.OrderStatusID,
          text = status.OrderStatusName
        });
      }

      return Content(JsonConvert.SerializeObject(listStatus), "application/json");
    }

    public ActionResult ShippingStatusOption()
    {
      var shippingStatus = Repository.Create<ShippingStatu>().FetchAll();
      var listStatus = new List<object>();
      foreach (var status in shippingStatus)
      {
        listStatus.Add(new
        {
          value = status.ShippingStatusID,
          text = status.ShippingName
        });
      }

      return Content(JsonConvert.SerializeObject(listStatus), "application/json");
    }

    [HttpPost]
    public ActionResult UpdateStatus(int? id, [Bind(Prefix = "order_status")] int? orderStatus = null,
      [Bind(Prefix = "shipping_status")] int? shippingStatus = null)
    {
      dynamic result = new ExpandoObject();
      result.status = false;
      result.message = "";
      if (id == null)
      {
        result.message = "Thiếu mã đơn đặt hàng";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      if (orderStatus == null && shippingStatus == null)
      {
        result.message = "Phải có ít nhất 1 trong 2 tham số orderStatus và shippingStatus";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      var order = Repository.Order.FindById(id);
      if (order == null)
      {
        result.message = "Đơn đặt hàng này không tồn tại trong hệ thống";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      int oldOderStatus = order.OrderStatusID;
      int oldShipping = order.ShippingStatusID;
      bool oldPaid = order.Paid;

      if (orderStatus != null)
      {
        var repo = Repository.Create<OrderStatu>();
        if (!repo.FetchAll().Any(s => s.OrderStatusID == orderStatus))
        {
          result.message = "Mã orderStatus không hợp lệ";
          return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        order.OrderStatusID = orderStatus.Value;
      }

      if (shippingStatus != null)
      {
        var repo = Repository.Create<ShippingStatu>();
        if (!repo.FetchAll().Any(s => s.ShippingStatusID == shippingStatus))
        {
          result.message = "Mã shippingStatus không hợp lệ";
          return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        order.ShippingStatusID = shippingStatus.Value;
      }

      Repository.Order.SaveChanges();
      
      // Reload order to get latest status after save
      order = Repository.Order.FindById(id);
      
      // Check if order is complete (all 3 conditions met) and deduct inventory
      bool isCompleteNow = (order.OrderStatusID == 3 && order.Paid && order.ShippingStatusID == 3);
      
      if (isCompleteNow)
      {
        // Kiểm tra xem đã trừ kho chưa
        if (!IsInventoryDeducted(order))
        {
          // Order is complete và chưa trừ - deduct inventory
          DeductProductInventory(order);
          
          // Lưu flag đã trừ vào Comment
          MarkInventoryDeducted(order);
          Repository.Order.SaveChanges();
        }
      }
      
      // Auto-update customer rank when order is completed AND paid
      if (orderStatus.HasValue && orderStatus.Value == 3 && oldOderStatus != 3 && order.Paid && order.CustomerID > 0)
      {
        // Order just completed and already paid - update customer rank
        var customer = Repository.Customer.FindById(order.CustomerID);
        if (customer != null)
        {
          customer.UpdateRankAndSpent(Repository);
          Repository.Customer.SaveChanges();
        }
      }
      
      result.status = true;
      result.message = "Update thành công";
      result.order_class_remove = getClassOrderStatus(oldOderStatus);
      result.order_class_add = getClassOrderStatus(order.OrderStatusID);
      return Content(JsonConvert.SerializeObject(result), "application/json");
    }

    public ActionResult UpdatePayment(int? id, bool paid = false)
    {
      dynamic result = new ExpandoObject();
      result.success = false;
      result.message = "";
      if (id == null)
      {
        result.message = "Thiếu mã đơn đặt hàng";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      var order = Repository.Order.FindById(id);
      if (order == null)
      {
        result.message = "Đơn đặt hàng này không tồn tại";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      bool wasUnpaid = !order.Paid;
      bool wasCompleteBefore = (order.OrderStatusID == 3 && order.Paid && order.ShippingStatusID == 3);
      order.Paid = paid;
      Repository.Order.SaveChanges();
      
      // Reload order to get latest status after save
      order = Repository.Order.FindById(id);
      
      // Check if order is complete (all 3 conditions met) and deduct inventory
      bool isCompleteNow = (order.OrderStatusID == 3 && order.Paid && order.ShippingStatusID == 3);
      
      if (isCompleteNow)
      {
        // Kiểm tra xem đã trừ kho chưa
        if (!IsInventoryDeducted(order))
        {
          // Order is complete và chưa trừ - deduct inventory
          DeductProductInventory(order);
          
          // Lưu flag đã trừ vào Comment
          MarkInventoryDeducted(order);
          Repository.Order.SaveChanges();
        }
      }
      
      // Auto-update customer rank when order is marked as paid AND completed
      if (paid && wasUnpaid && order.OrderStatusID == 3 && order.CustomerID > 0)
      {
        var customer = Repository.Customer.FindById(order.CustomerID);
        if (customer != null)
        {
          customer.UpdateRankAndSpent(Repository);
          Repository.Customer.SaveChanges();
        }
      }
      
      result.success = true;
      result.message = "Cập nhật thành công";
      return Content(JsonConvert.SerializeObject(result), "application/json");
    }

    public ActionResult RenoveProductOrder(OrderDetail orderDetail)
    {
      dynamic result = new ExpandoObject();
      result.status = "error";
      result.title = "Loại bỏ thất bại";
      result.message = "";
      if (orderDetail.DetailID == 0 || orderDetail.OrderID == 0)
      {
        result.message = "Thiếu thông số";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      var order = Repository.Order.FindById(orderDetail.OrderID);
      if (order == null)
      {
        result.message = "Đơn đặt hàng không tồn tại";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      var oldDetail = order.OrderDetails.FirstOrDefault(o => o.DetailID == orderDetail.DetailID);
      if (oldDetail == null)
      {
        result.message = "Sản phẩm này không tồn tại trong đơn đặt hàng";
        return Content(JsonConvert.SerializeObject(result), "application/json");
      }

      order.OrderDetails.Remove(oldDetail);
      var sum = order.OrderDetails.Sum(o => o.Total);
      order.TotalPrice = sum;
      Repository.Order.SaveChanges();

      result.status = "success";
      result.title = "Loại bỏ thành công";
      result.message = "Chúc mừng bạn đã loại bỏ sản phẩm khỏi đơn đặt hàng thành công";
      return Content(JsonConvert.SerializeObject(result), "application/json");
    }

    public ActionResult Delete(int? id)
    {
      if (id == null)
      {
        return HttpNotFound();
      }

      var order = Repository.Order.FindById(id);
      if (order == null)
        return RedirectToAction("Index", "Order");
      Repository.Order.Delete(id);
      Repository.SaveChanges();
      return RedirectToAction("Index", "Order");
    }

    private String getClassOrderStatus(int status)
    {
      var statusColor = "";
      switch (status)
      {
        case 1:
          statusColor = "warning";
          break;

        case 2:
          statusColor = "info";
          break;

        case 3:
          statusColor = "success";
          break;

        default:
          statusColor = "danger";
          break;
      }

      return "label-" + statusColor;
    }

    /// <summary>
    /// Kiểm tra xem đơn hàng đã được trừ số lượng sản phẩm chưa
    /// </summary>
    private bool IsInventoryDeducted(Order order)
    {
      const string INVENTORY_DEDUCTED_FLAG = "[INVENTORY_DEDUCTED]";
      return !string.IsNullOrEmpty(order.Comment) && order.Comment.Contains(INVENTORY_DEDUCTED_FLAG);
    }

    /// <summary>
    /// Đánh dấu đơn hàng đã được trừ số lượng sản phẩm
    /// </summary>
    private void MarkInventoryDeducted(Order order)
    {
      const string INVENTORY_DEDUCTED_FLAG = "[INVENTORY_DEDUCTED]";
      if (string.IsNullOrEmpty(order.Comment))
      {
        order.Comment = INVENTORY_DEDUCTED_FLAG;
      }
      else if (!order.Comment.Contains(INVENTORY_DEDUCTED_FLAG))
      {
        order.Comment = order.Comment.Trim() + " " + INVENTORY_DEDUCTED_FLAG;
      }
    }

    /// <summary>
    /// Trừ số lượng sản phẩm từ kho khi đơn hàng hoàn thành (cả 3 trạng thái đều thành công)
    /// </summary>
    private void DeductProductInventory(Order order)
    {
      try
      {
        var db = Repository.DbContext;
        
        System.Diagnostics.Debug.WriteLine($"=== Deducting inventory for Order #{order.OrderID} ===");
        System.Diagnostics.Debug.WriteLine($"OrderStatusID: {order.OrderStatusID}, Paid: {order.Paid}, ShippingStatusID: {order.ShippingStatusID}");
        
        // Load OrderDetails với Product để kiểm tra UseMultiColor
        var orderDetails = db.Set<OrderDetail>()
          .Where(od => od.OrderID == order.OrderID)
          .Include(od => od.Product)
          .ToList();

        System.Diagnostics.Debug.WriteLine($"Found {orderDetails.Count} order details");

        foreach (var orderDetail in orderDetails)
        {
          var product = orderDetail.Product;
          if (product == null)
          {
            System.Diagnostics.Debug.WriteLine($"OrderDetail {orderDetail.DetailID}: Product is null, skipping");
            continue;
          }

          int quantityToDeduct = orderDetail.Quantity;
          System.Diagnostics.Debug.WriteLine($"Processing: ProductID={product.ProductID}, Quantity={quantityToDeduct}, UseMultiColor={product.UseMultiColor}, ColorID={orderDetail.ColorID}");

          // Luôn trừ từ Product.Stock (số lượng tổng) vì đây là số lượng hiển thị trong Admin/Product
          var productToUpdate = Repository.Product.FindById(product.ProductID);
          if (productToUpdate != null)
          {
            var oldStock = productToUpdate.Stock;
            productToUpdate.Stock = Math.Max(0, productToUpdate.Stock - quantityToDeduct);
            Repository.Product.SaveChanges();
            System.Diagnostics.Debug.WriteLine($"Product Stock - Old: {oldStock}, New: {productToUpdate.Stock}, Deducted: {quantityToDeduct}");
            
            // Nếu sản phẩm có nhiều màu và có ColorID, cũng trừ từ bảng Quantity (nếu có)
            if (product.UseMultiColor && orderDetail.ColorID.HasValue)
            {
              var colorId = orderDetail.ColorID.Value;
              
              // Kiểm tra xem có record trong Quantity chưa
              var checkSql = "SELECT COUNT(*) FROM Quantity WHERE ProductID = @p0 AND ColorID = @p1";
              var exists = db.Database.SqlQuery<int>(checkSql, product.ProductID, colorId).FirstOrDefault() > 0;
              
              if (exists)
              {
                // Có record - cập nhật Stock trong Quantity
                var updateSql = @"
                  UPDATE Quantity 
                  SET Stock = CASE 
                    WHEN Stock - @p0 < 0 THEN 0 
                    ELSE Stock - @p0 
                  END
                  WHERE ProductID = @p1 AND ColorID = @p2";
                
                var rowsAffected = db.Database.ExecuteSqlCommand(updateSql, quantityToDeduct, product.ProductID, colorId);
                System.Diagnostics.Debug.WriteLine($"Quantity table also updated. Rows affected: {rowsAffected}");
              }
            }
          }
          else
          {
            System.Diagnostics.Debug.WriteLine($"Product {product.ProductID} not found, skipping");
          }
        }
        
        System.Diagnostics.Debug.WriteLine($"=== Finished deducting inventory for Order #{order.OrderID} ===");
      }
      catch (Exception ex)
      {
        // Log error nhưng không throw để không ảnh hưởng đến việc cập nhật trạng thái đơn hàng
        System.Diagnostics.Debug.WriteLine("Error deducting product inventory: " + ex.Message);
        System.Diagnostics.Debug.WriteLine("StackTrace: " + ex.StackTrace);
        // Có thể log vào file log ở đây nếu cần
      }
    }
  }
}

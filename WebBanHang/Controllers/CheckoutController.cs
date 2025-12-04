using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.ViewModels;

namespace WebBanLinhKienDienTu.Controllers
{
  [Authorize]
  public class CheckoutController : BaseController
  {
    //
    // GET: /Checkout/
    public ActionResult Index()
    {
      return RedirectToAction("Shipping");
    }

    [HttpGet]
    public ActionResult Shipping()
    {
      if (Cart.GetCount() == 0)
        return RedirectToAction("Index", "Cart");
      ViewData["Provinces"] = Repository.Province.FetchAll().OrderBy(i => i.Type + " " + i.ProvinceName).ToList();
      return View();
    }

    [HttpPost]
    public ActionResult Shipping(ShippingViewModel model)
    {
      if (!ModelState.IsValid)
      {
        ViewData["Provinces"] = Repository.Province.FetchAll().OrderBy(i => i.Type + " " + i.ProvinceName).ToList();
        return View(model);
      }

      model.Province = Repository.Province.FindById(model.ProvinceID);
      model.District = Repository.District.FindById(model.DistrictID);
      model.Ward = Repository.Ward.FindById(model.WardID);

      if (model.Province == null || model.District == null || model.Ward == null)
      {
        ModelState.AddModelError("", "Vui lòng chọn địa chỉ giao hàng!");
        ViewData["Provinces"] = Repository.Province.FetchAll().OrderBy(i => i.Type + " " + i.ProvinceName).ToList();
        return View(model);
      }

      TempData["Ship"] = model;
      return RedirectToAction("Payment", "Checkout");
    }

    [HttpGet]
    public ActionResult Payment()
    {
      if (TempData["Ship"] == null)
      {
        return RedirectToAction("Shipping", "Checkout");
      }

      var payments = Repository.Payment.FetchAll();
      ViewData["pCod"] = payments.SingleOrDefault(p => p.PaymentType.Equals("cod"));
      ViewData["pAtm"] = payments.SingleOrDefault(p => p.PaymentType.Equals("atm"));
      ViewData["pOnline"] = payments.SingleOrDefault(p => p.PaymentType.Equals("online"));
      ViewData["pMomo"] = payments.SingleOrDefault(p => p.PaymentType.Equals("momo"));
      var ship = (ShippingViewModel)TempData["Ship"];
      return View(ship);
    }

    [HttpPost]
    public ActionResult Payment(ShippingViewModel model)
    {
      if (ModelState.IsValid)
      {
        try
        {
          var customer = UserManager.CurrentCustomer;
          var subtotal = ShoppingCart.Instance.GetTotal();
          long rankDiscount = 0;
          long couponDiscount = 0;
          string couponCode = "";
          
          if (customer != null && customer.MemberRank != null && customer.MemberRank.DiscountPercent > 0)
          {
            rankDiscount = (long)(subtotal * (decimal)customer.MemberRank.DiscountPercent / 100M);
          }
          
          if (Session["CouponCode"] != null)
          {
            couponCode = Session["CouponCode"].ToString();
            if (Session["CouponDiscount"] != null)
            {
              couponDiscount = Convert.ToInt64(Session["CouponDiscount"]);
            }
          }
          
          var totalDiscount = rankDiscount + couponDiscount;
          var totalPrice = subtotal - totalDiscount;
          if (totalPrice < 0) 
          {
            totalPrice = 0;
          }
        
        var order = new Order
        {
          CustomerID = model.CustomerID,
          PaymentID = model.PaymentMethod,
          OrderDate = DateTime.Now,
          FullName = model.FullName,
          Address = model.Address,
          ProvinceID = model.ProvinceID,
          DistrictID = model.DistrictID,
          WardID = model.WardID,
          Phone = model.Phone,
          TotalPrice = totalPrice,
          Discount = totalDiscount,
          CouponCode = string.IsNullOrEmpty(couponCode) ? null : couponCode,
          Paid = false,
          OrderStatusID = 1,
          ShippingStatusID = 1,
          Comment = model.Comment
        };
        var payment = Repository.Payment.FindById(model.PaymentMethod);

        if (payment == null)
        {
          ModelState.AddModelError("PaymentMethod", "Phương thức thanh toán không hợp lệ!");
          model.Province = Repository.Province.FindById(model.ProvinceID);
          model.District = Repository.District.FindById(model.DistrictID);
          model.Ward = Repository.Ward.FindById(model.WardID);
          var paymentList = Repository.Payment.FetchAll();
          ViewData["pCod"] = paymentList.SingleOrDefault(p => p.PaymentType.Equals("cod"));
          ViewData["pMomo"] = paymentList.SingleOrDefault(p => p.PaymentType.Equals("momo"));
          return View(model);
        }

        if (payment.PaymentType.Equals("cod"))
        {
          var newOrder = Repository.Order.Insert(order);
          Repository.Order.SaveChanges();

          if (newOrder != null && newOrder.OrderID != 0)
          {
            var detailRepo = Repository.Create<OrderDetail>();
            foreach (var cart in ShoppingCart.Instance.Items)
            {
              var od = new OrderDetail
              {
                OrderID = newOrder.OrderID,
                ProductID = cart.ProductID,
                Price = cart.Price,
                Quantity = (byte)cart.Quantity,
                ColorID = cart.ColorID,
                Total = cart.TotalPrice
              };
              detailRepo.Insert(od);
            }

            Repository.SaveChanges();
            ShoppingCart.Instance.Clean();
            
            Session.Remove("CouponCode");
            Session.Remove("CouponDiscount");
            Session.Remove("CouponFreeShip");
            
            TempData["ship"] = newOrder;
            return RedirectToAction("Success", "Checkout");
          }

          ModelState.AddModelError("PaymentMethod", "Đã xảy ra lỗi, không thể đặt hàng!!!");
        }
        else if (payment.PaymentType.Equals("momo"))
        {
          TempData["Ship"] = model;
          Session["PendingOrder"] = order;
          return RedirectToAction("MomoPayment", "Checkout");
        }
        }
        catch (System.Exception ex)
        {
          ModelState.AddModelError("", "Lỗi thanh toán: " + ex.Message);
          if (ex.InnerException != null)
          {
            ModelState.AddModelError("", "Chi tiết: " + ex.InnerException.Message);
          }
        }
      }

      if (model != null)
      {
        model.Province = Repository.Province.FindById(model.ProvinceID);
        model.District = Repository.District.FindById(model.DistrictID);
        model.Ward = Repository.Ward.FindById(model.WardID);
      }
      var payments = Repository.Payment.FetchAll();
      ViewData["pCod"] = payments.SingleOrDefault(p => p.PaymentType.Equals("cod"));
      // ViewData["pAtm"] = payments.SingleOrDefault(p => p.PaymentType.Equals("atm"));
      // ViewData["pOnline"] = payments.SingleOrDefault(p => p.PaymentType.Equals("online"));
      ViewData["pMomo"] = payments.SingleOrDefault(p => p.PaymentType.Equals("momo"));
      return View(model);
    }

    // public ActionResult OnePayNoiDia()
    // {
    //   string amount = (ShoppingCart.Instance.GetTotal() * 100).ToString();
    //   // Khoi tao lop thu vien
    //   VPCRequest conn = new VPCRequest(OnepayProperty.URL_ONEPAY_TEST);
    //   conn.SetSecureSecret(OnepayProperty.HASH_CODE);
    //
    //   conn.AddDigitalOrderField("Title", "Thanh toán trực tuyến");
    //   conn.AddDigitalOrderField("vpc_Locale", "vn"); //Chon ngon ngu hien thi tren cong thanh toan (vn/en)
    //   conn.AddDigitalOrderField("vpc_Version", OnepayProperty.VERSION);
    //   conn.AddDigitalOrderField("vpc_Command", OnepayProperty.COMMAND);
    //   conn.AddDigitalOrderField("vpc_Merchant", OnepayProperty.MERCHANT_ID);
    //   conn.AddDigitalOrderField("vpc_AccessCode", OnepayProperty.ACCESS_CODE);
    //   conn.AddDigitalOrderField("vpc_MerchTxnRef", RandomString());
    //   conn.AddDigitalOrderField("vpc_Amount", amount);
    //   conn.AddDigitalOrderField("vpc_Currency", "VND");
    //   conn.AddDigitalOrderField("vpc_ReturnURL",
    //     Url.Action("OnePayNoiDiaRes", "CheckOut", null, Request.Url.Scheme, null));
    //
    //   // Thong tin them ve khach hang. De trong neu khong co thong tin
    //   conn.AddDigitalOrderField("vpc_SHIP_Street01", "");
    //   conn.AddDigitalOrderField("vpc_SHIP_Provice", "");
    //   conn.AddDigitalOrderField("vpc_SHIP_City", "");
    //   conn.AddDigitalOrderField("vpc_SHIP_Country", "");
    //   conn.AddDigitalOrderField("vpc_Customer_Phone", "");
    //   conn.AddDigitalOrderField("vpc_Customer_Email", "");
    //   conn.AddDigitalOrderField("vpc_Customer_Id", "");
    //   conn.AddDigitalOrderField("vpc_TicketNo", Request.UserHostAddress);
    //
    //   string url = conn.Create3PartyQueryString();
    //   return Redirect(url);
    // }

    // public ActionResult OnePayNoiDiaRes()
    // {
    //   string hashvalidateResult = "";
    //
    //   // Khoi tao lop thu vien
    //   VPCRequest conn = new VPCRequest(OnepayProperty.URL_ONEPAY_TEST);
    //   conn.SetSecureSecret(OnepayProperty.HASH_CODE);
    //
    //   // Xu ly tham so tra ve va du lieu ma hoa
    //   hashvalidateResult = conn.Process3PartyResponse(Request.QueryString);
    //
    //   // Lay tham so tra ve tu cong thanh toan
    //   string vpc_TxnResponseCode = conn.GetResultField("vpc_TxnResponseCode");
    //   string amount = conn.GetResultField("vpc_Amount");
    //   string localed = conn.GetResultField("vpc_Locale");
    //   string command = conn.GetResultField("vpc_Command");
    //   string version = conn.GetResultField("vpc_Version");
    //   string cardType = conn.GetResultField("vpc_Card");
    //   string orderInfo = conn.GetResultField("vpc_OrderInfo");
    //   string merchantID = conn.GetResultField("vpc_Merchant");
    //   string authorizeID = conn.GetResultField("vpc_AuthorizeId");
    //   string merchTxnRef = conn.GetResultField("vpc_MerchTxnRef");
    //   string transactionNo = conn.GetResultField("vpc_TransactionNo");
    //   string acqResponseCode = conn.GetResultField("vpc_AcqResponseCode");
    //   string txnResponseCode = vpc_TxnResponseCode;
    //   string message = conn.GetResultField("vpc_Message");
    //
    //   // Kiem tra 2 tham so tra ve quan trong nhat
    //   if (hashvalidateResult.Equals("CORRECTED") && txnResponseCode.Trim() == "0")
    //   {
    //     return Content("PaySuccess");
    //   }
    //   else if (hashvalidateResult == "INVALIDATED" && txnResponseCode.Trim() == "0")
    //   {
    //     return Content("PayPending");
    //   }
    //   else
    //   {
    //     return Content("PayUnSuccess");
    //   }
    // }

    public ActionResult MomoPayment()
    {
      string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
      string partnerCode = "MOMOLRJZ20181206";
      string accessKey = "mTCKt9W3eU1m39TW";
      string secretKey = "SetA5RDnLHvt51AULf51DyauxUo3kDU6";
      string orderInfo = "Thanh toán đơn hàng";
      string redirectUrl = Url.Action("MomoPaymentResponse", "Checkout", null, Request.Url.Scheme);
      string ipnUrl = Url.Action("Payment", "Checkout", null, Request.Url.Scheme);
      string requestId = Guid.NewGuid().ToString();
      string orderId = Guid.NewGuid().ToString();
      
      var customer = UserManager.CurrentCustomer;
      var subtotal = ShoppingCart.Instance.GetTotal();
      long rankDiscount = 0;
      long couponDiscount = 0;
      
      if (customer != null && customer.MemberRank != null && customer.MemberRank.DiscountPercent > 0)
      {
        rankDiscount = (long)(subtotal * (decimal)customer.MemberRank.DiscountPercent / 100M);
      }
      
      if (Session["CouponDiscount"] != null)
      {
        couponDiscount = Convert.ToInt64(Session["CouponDiscount"]);
      }
      
      long finalAmount = subtotal - rankDiscount - couponDiscount;
      if (finalAmount < 0) 
      {
        finalAmount = 0;
      }
      
      const long MOMO_MIN_AMOUNT = 10000;
      const long MOMO_MAX_AMOUNT = 50000000;
      
      if (finalAmount < MOMO_MIN_AMOUNT)
      {
        TempData["ErrorMessage"] = $"Số tiền thanh toán ({finalAmount:N0}d) nhỏ hơn mức tối thiểu {MOMO_MIN_AMOUNT:N0}d của Momo. Vui lòng chọn phương thức thanh toán khác.";
        TempData.Keep("Ship");
        Session.Remove("PendingOrder");
        return RedirectToAction("Payment", "Checkout");
      }
      
      if (finalAmount > MOMO_MAX_AMOUNT)
      {
        TempData["ErrorMessage"] = $"Số tiền thanh toán ({finalAmount:N0}d) vuợt quá mức tối đa {MOMO_MAX_AMOUNT:N0}d của Momo. Vui lòng chọn phương thức thanh toán khác.";
        TempData.Keep("Ship");
        Session.Remove("PendingOrder");
        return RedirectToAction("Payment", "Checkout");
      }
      
      string amount = finalAmount.ToString();
      string requestType = "payWithATM";
      string extraData = "";

      string rawHash = "accessKey=" + accessKey +
                       "&amount=" + amount +
                       "&extraData=" + extraData +
                       "&ipnUrl=" + ipnUrl +
                       "&orderId=" + orderId +
                       "&orderInfo=" + orderInfo +
                       "&partnerCode=" + partnerCode +
                       "&redirectUrl=" + redirectUrl +
                       "&requestId=" + requestId +
                       "&requestType=" + requestType;
      
      string signature = GenerateSignature(rawHash, secretKey);

      var paymentData = new
      {
        partnerCode,
        requestId,
        amount,
        orderId,
        orderInfo,
        redirectUrl,
        ipnUrl,
        extraData = "",
        requestType,
        signature,
      };
      
      try
      {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (HttpClient client = new HttpClient())
        {
          var response = client.PostAsJsonAsync(endpoint, paymentData).Result;
          var responseContent = response.Content.ReadAsStringAsync().Result;
          dynamic responseObject = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);
          
          int resultCode = responseObject.resultCode ?? -1;
          if (resultCode != 0)
          {
            string errorMessage = responseObject.message ?? "Lỗi kết nối với công thanh toán Momo";
            TempData["ErrorMessage"] = "Thanh toán thất bại: " + errorMessage;
            TempData.Keep("Ship");
            Session.Remove("PendingOrder");
            return RedirectToAction("Payment", "Checkout");
          }
          
          string payUrl = responseObject.payUrl;
          if (string.IsNullOrEmpty(payUrl))
          {
            TempData["ErrorMessage"] = "Không thể tạo link thanh toán. Vui lòng thử lại sau.";
            TempData.Keep("Ship");
            Session.Remove("PendingOrder");
            return RedirectToAction("Payment", "Checkout");
          }
          
          return Redirect(payUrl);
        }
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = "Lỗi thanh toán Momo: " + ex.Message;
        TempData.Keep("Ship");
        Session.Remove("PendingOrder");
        return RedirectToAction("Payment", "Checkout");
      }
    }

    public ActionResult MomoPaymentResponse()
    {
      string partnerCode = Request.QueryString["partnerCode"];
      string orderId = Request.QueryString["orderId"];
      string requestId = Request.QueryString["requestId"];
      string amount = Request.QueryString["amount"];
      string orderInfo = Request.QueryString["orderInfo"];
      string orderType = Request.QueryString["orderType"];
      string transId = Request.QueryString["transId"];
      string resultCode = Request.QueryString["resultCode"];
      string message = Request.QueryString["message"];
      string payType = Request.QueryString["payType"];
      string responseTime = Request.QueryString["responseTime"];
      string extraData = Request.QueryString["extraData"];
      string signature = Request.QueryString["signature"];

      // Tạo chuỗi để xác minh chữ ký
      string rawHash =
        $"amount={amount}&extraData={extraData}&message={message}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&payType={payType}&requestId={requestId}&responseTime={responseTime}&resultCode={resultCode}";
      string secretKey = "SetA5RDnLHvt51AULf51DyauxUo3kDU6";
      string calculatedSignature = GenerateSignature(rawHash, secretKey);

      if (resultCode == "0")
      {
        Order order = Session["PendingOrder"] as Order;
        
        if (order != null)
        {
          order.Paid = true;
          var newOrder = Repository.Order.Insert(order);
          Repository.Order.SaveChanges();

          if (newOrder != null && newOrder.OrderID != 0)
          {
            var detailRepo = Repository.Create<OrderDetail>();
            foreach (var cart in ShoppingCart.Instance.Items)
            {
              var od = new OrderDetail
              {
                OrderID = newOrder.OrderID,
                ProductID = cart.ProductID,
                Price = cart.Price,
                Quantity = (byte)cart.Quantity,
                ColorID = cart.ColorID,
                Total = cart.TotalPrice
              };
              detailRepo.Insert(od);
            }

            Repository.SaveChanges();
            
            if (order.CustomerID > 0)
            {
              var customer = Repository.Customer.FindById(order.CustomerID);
              if (customer != null)
              {
                customer.UpdateRankAndSpent(Repository);
                Repository.Customer.Update(customer);
                Repository.SaveChanges();
              }
            }

            ShoppingCart.Instance.Clean();
            Session.Remove("CouponCode");
            Session.Remove("CouponDiscount");
            Session.Remove("CouponFreeShip");
            Session.Remove("PendingOrder");

            TempData["ship"] = newOrder;
            return RedirectToAction("Success", "Checkout");
          }

          Session.Remove("PendingOrder");
          TempData["ErrorMessage"] = "Thanh toán thành công nhưng không thể lưu đơn hàng. Vui lòng liên hệ CSKH.";
          return RedirectToAction("Index", "Home");
        }

        Session.Remove("PendingOrder");
        TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn. Vui lòng đặt hàng lại.";
        return RedirectToAction("Index", "Cart");
      }
      else
      {
        Session.Remove("PendingOrder");
        TempData["ErrorMessage"] = $"Thanh toán thất bại: {message}. Vui lòng thử lại.";
        return RedirectToAction("Index", "Cart");
      }
    }

    private string GenerateSignature(string data, string key)
    {
      var encoding = new System.Text.UTF8Encoding();
      byte[] keyByte = encoding.GetBytes(key);
      byte[] messageBytes = encoding.GetBytes(data);

      using (var hmacsha256 = new HMACSHA256(keyByte))
      {
        byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
        return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
      }
    }


    private string RandomString()
    {
      var str = new StringBuilder();
      var random = new Random();
      for (int i = 0; i <= 5; i++)
      {
        var c = Convert.ToChar(Convert.ToInt32(random.Next(65, 68)));
        str.Append(c);
      }

      return str.ToString().ToLower();
    }

    public ActionResult Success()
    {
      if (TempData["ship"] == null)
      {
        return RedirectToAction("Index", "Home");
      }

      var model = (Order)TempData["ship"];
      // Gửi API vận chuyển
      // var shippingResult = ShippingApi(model);
      //
      // // Kiểm tra kết quả API
      // if (shippingResult.IsSuccess)
      // {
      //   // Thành công
      //   TempData["Message"] = "Đơn hàng đã được xử lý và gửi đến đơn vị vận chuyển!";
      // }
      // else
      // {
      //   // Thất bại
      //   TempData["Message"] = "Đơn hàng đã được xử lý nhưng gửi vận chuyển thất bại. Vui lòng kiểm tra lại!";
      // }
      return View("Checkout_Success", model);
    }
  }
}
using System;
using System.Net.Http;
using System.Threading.Tasks;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Controllers
{
  public class ShippingApi
  {
    private HttpClient httpClient = new HttpClient();
    private string url = "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/create";
    private string token = "e5048e0e-d6d7-11ef-95b5-462e8a9fd93d";

    public ShippingApi()
    {
      httpClient.BaseAddress = new Uri(url);
      httpClient.DefaultRequestHeaders.Accept.Clear();
      httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> ShippingService(Order order)
    {
      var client = new HttpClient();
      client.DefaultRequestHeaders.Add(token, "e5048e0e-d6d7-11ef-95b5-462e8a9fd93d");
      
      var payload = new
      {
        shop_id = "4635855",
        from_district_id = "1442",//id quan 1 tphcm
        to_district_id = order.DistrictID,
        //weight = order.Weight,
        //length = order.Length,
        //width = order.Width,
        //height = order.Height,
        payment_type_id = 2, // Dịch vụ giao hàng
        to_name = order.Customer,
        to_phone = order.Phone,
        to_address = order.Address,
        //tokenapi:e5048e0e-d6d7-11ef-95b5-462e8a9fd93d
      };

      var response = await client.PostAsJsonAsync("https://api.ghn.vn/v2/create_order", payload);
      return await response.Content.ReadAsStringAsync();
    }
  }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebBanLinhKienDienTu.Utils
{
    public static class UrlHelpers
    {
        public static String QueryString(this UrlHelper url, String key, String value)
        {
            String qString = HttpContext.Current.Request.QueryString.ToString();
            Dictionary<String, object> dict = new Dictionary<string, object>();
            HttpContext.Current.Request.QueryString.CopyTo(dict);
            dict.Remove("page"); // Xóa page khi thay đổi filter/sort
            
            if (string.IsNullOrEmpty(value) || value == "default")
            {
                // Nếu value rỗng hoặc "default", xóa key đó
                dict.Remove(key);
            }
            else
            {
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, value);
                }
                else
                {
                    dict[key] = value;
                }
            }

            return string.Join("&", dict.Select(x => x.Key + "=" + HttpUtility.UrlEncode(x.Value.ToString())).ToArray());
        }

        public static String QueryString(this UrlHelper url)
        {
            String qString = HttpContext.Current.Request.QueryString.ToString();
            Dictionary<String, object> dict = new Dictionary<string, object>();
            HttpContext.Current.Request.QueryString.CopyTo(dict);
            return string.Join("&", dict.Select(x => x.Key + "=" + x.Value).ToArray());
        }
    }
}
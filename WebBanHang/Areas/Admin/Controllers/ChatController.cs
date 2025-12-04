using System;
using System.Linq;
using System.Web.Mvc;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Core;

namespace WebBanLinhKienDienTu.Areas.Admin.Controllers
{
    // Helper classes cho raw SQL query
    public class CustomerChatInfo
    {
        public int CustomerID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string LastMessage { get; set; }
        public DateTime? LastMessageDate { get; set; }
        public int UnreadCount { get; set; }
    }

    public class ChatMessageInfo
    {
        public int MessageID { get; set; }
        public string Message { get; set; }
        public bool IsFromAdmin { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    [Security]
    public class ChatController : AdminBaseController
    {
        // GET: Admin/Chat
        public ActionResult Index()
        {
            return View();
        }

        // GET: Admin/Chat/GetCustomers
        [HttpPost]
        public JsonResult GetCustomers()
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    // Sử dụng raw SQL vì ChatMessage chưa có trong EDMX
                    var sql = @"
                        SELECT 
                            c.CustomerID,
                            c.FullName,
                            c.Email,
                            c.Phone,
                            cm.Message AS LastMessage,
                            cm.CreatedDate AS LastMessageDate,
                            (SELECT COUNT(*) FROM ChatMessage cm2 
                             WHERE cm2.CustomerID = c.CustomerID 
                             AND cm2.IsRead = 0 
                             AND cm2.IsFromAdmin = 0) AS UnreadCount
                        FROM Customers c
                        INNER JOIN (
                            SELECT CustomerID, 
                                   Message, 
                                   CreatedDate,
                                   ROW_NUMBER() OVER (PARTITION BY CustomerID ORDER BY CreatedDate DESC) AS rn
                            FROM ChatMessage
                            WHERE IsFromAdmin = 0
                        ) cm ON c.CustomerID = cm.CustomerID AND cm.rn = 1
                        ORDER BY cm.CreatedDate DESC";

                    var customers = db.Database.SqlQuery<CustomerChatInfo>(sql).ToList();

                    return Json(new { success = true, data = customers }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Admin/Chat/GetMessages
        [HttpPost]
        public JsonResult GetMessages(int customerId)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    System.Diagnostics.Debug.WriteLine("GetMessages: Loading messages for CustomerID: " + customerId);
                    
                    // Sử dụng raw SQL vì ChatMessage chưa có trong EDMX
                    // Chuyển đổi IsFromAdmin từ bit sang int để đảm bảo mapping đúng
                    var sql = @"
                        SELECT 
                            MessageID,
                            Message,
                            CAST(IsFromAdmin AS BIT) AS IsFromAdmin,
                            CreatedDate
                        FROM ChatMessage
                        WHERE CustomerID = @p0
                        ORDER BY CreatedDate ASC";

                    var messages = db.Database.SqlQuery<ChatMessageInfo>(sql, customerId).ToList();
                    
                    System.Diagnostics.Debug.WriteLine("GetMessages: Found " + messages.Count + " message(s)");
                    foreach (var msg in messages)
                    {
                        System.Diagnostics.Debug.WriteLine("  - MessageID: " + msg.MessageID + ", IsFromAdmin: " + msg.IsFromAdmin + ", Message: " + (msg.Message ?? "").Substring(0, Math.Min(50, msg.Message?.Length ?? 0)));
                    }

                    // Đánh dấu đã đọc (chỉ tin nhắn từ khách hàng)
                    var updateSql = @"
                        UPDATE ChatMessage 
                        SET IsRead = 1 
                        WHERE CustomerID = @p0 
                        AND IsRead = 0 
                        AND IsFromAdmin = 0";
                    db.Database.ExecuteSqlCommand(updateSql, customerId);

                    return Json(new { success = true, data = messages }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetMessages error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack trace: " + ex.StackTrace);
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Admin/Chat/SendMessage
        [HttpPost]
        public JsonResult SendMessage(int customerId, string message)
        {
            try
            {
                if (customerId <= 0 || string.IsNullOrWhiteSpace(message))
                {
                    return Json(new { success = false, message = "CustomerID hoặc tin nhắn không hợp lệ" }, JsonRequestBehavior.AllowGet);
                }

                using (var db = new ecommerceEntities())
                {
                    var customer = db.Customers.Find(customerId);
                    if (customer == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy khách hàng" }, JsonRequestBehavior.AllowGet);
                    }

                    System.Diagnostics.Debug.WriteLine("ChatController SendMessage: Executing SQL INSERT for CustomerID: " + customerId);
                    System.Diagnostics.Debug.WriteLine("ChatController SendMessage: Message: " + message);
                    
                    // Sử dụng raw SQL để insert vì ChatMessage chưa có trong EDMX
                    // Tạo bảng tạm để lưu MessageID từ OUTPUT clause
                    var messageId = 0;
                    try
                    {
                        // Cách 1: Dùng SqlQuery với OUTPUT (giống ChatHub)
                        var sql = @"
                            INSERT INTO ChatMessage (CustomerID, Message, IsFromAdmin, IsRead, CreatedDate)
                            OUTPUT INSERTED.MessageID
                            VALUES (@p0, @p1, @p2, @p3, @p4);";

                        var result = db.Database.SqlQuery<int>(sql, 
                            customerId,
                            message.Trim(),
                            1, // IsFromAdmin = 1 (true) - dùng int thay vì bool để tránh lỗi mapping
                            0, // IsRead = 0 (false)
                            DateTime.Now
                        ).ToList(); // Dùng ToList() thay vì FirstOrDefault() để đảm bảo query được execute
                        
                        messageId = result.FirstOrDefault();
                        
                        System.Diagnostics.Debug.WriteLine("ChatController SendMessage: Message inserted with ID: " + messageId);
                        
                        // Verify: Kiểm tra lại xem tin nhắn đã được lưu chưa
                        if (messageId > 0)
                        {
                            var verifySql = @"
                                SELECT MessageID, IsFromAdmin, Message, CreatedDate
                                FROM ChatMessage 
                                WHERE MessageID = @p0";
                            var verify = db.Database.SqlQuery<dynamic>(verifySql, messageId).FirstOrDefault();
                            if (verify != null)
                            {
                                System.Diagnostics.Debug.WriteLine("ChatController SendMessage: Verified - MessageID: " + verify.MessageID + ", IsFromAdmin: " + verify.IsFromAdmin + ", Message: " + verify.Message);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("ChatController SendMessage: WARNING - Could not verify message insertion!");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("ChatController SendMessage: ERROR - MessageID is 0, insertion may have failed!");
                        }
                    }
                    catch (Exception sqlEx)
                    {
                        System.Diagnostics.Debug.WriteLine("ChatController SendMessage SQL error: " + sqlEx.Message);
                        System.Diagnostics.Debug.WriteLine("Stack trace: " + sqlEx.StackTrace);
                        if (sqlEx.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Inner exception: " + sqlEx.InnerException.Message);
                        }
                        throw;
                    }
                    
                    return Json(new { success = true, messageId = messageId }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatController SendMessage error: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi gửi tin nhắn: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Admin/Chat/DeleteChatHistory
        [HttpPost]
        public JsonResult DeleteChatHistory(int customerId)
        {
            try
            {
                if (customerId <= 0)
                {
                    return Json(new { success = false, message = "CustomerID không hợp lệ" }, JsonRequestBehavior.AllowGet);
                }

                using (var db = new ecommerceEntities())
                {
                    // Xóa tất cả tin nhắn của khách hàng này
                    var deleteSql = @"DELETE FROM ChatMessage WHERE CustomerID = @p0";
                    var rowsAffected = db.Database.ExecuteSqlCommand(deleteSql, customerId);
                    
                    System.Diagnostics.Debug.WriteLine("DeleteChatHistory: Deleted " + rowsAffected + " message(s) for CustomerID: " + customerId);
                    
                    return Json(new { success = true, message = "Đã xóa " + rowsAffected + " tin nhắn", rowsAffected = rowsAffected }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatController DeleteChatHistory error: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi xóa lịch sử chat: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}


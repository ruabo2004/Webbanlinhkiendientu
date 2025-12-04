using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Core;

namespace WebBanLinhKienDienTu.Hubs
{
    // Helper classes cho raw SQL query
    public class ChatMessageData
    {
        public int MessageID { get; set; }
        public string Message { get; set; }
        public bool IsFromAdmin { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CustomerChatData
    {
        public int CustomerID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string LastMessage { get; set; }
        public DateTime? LastMessageDate { get; set; }
        public int UnreadCount { get; set; }
    }
    [HubName("chatHub")]
    public class ChatHub : Hub
    {
        private static readonly Dictionary<string, string> _userConnections = new Dictionary<string, string>();
        private static readonly Dictionary<int, string> _customerConnections = new Dictionary<int, string>();
        
        // Helper method để ghi log vào file
        private void WriteToFile(string message)
        {
            try
            {
                var appDataPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "App_Data");
                if (!System.IO.Directory.Exists(appDataPath))
                {
                    System.IO.Directory.CreateDirectory(appDataPath);
                }
                var logPath = System.IO.Path.Combine(appDataPath, "chat_debug.log");
                var logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " - " + message + Environment.NewLine;
                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch (Exception ex)
            {
                // Nếu không ghi được file, chỉ log vào Debug
                System.Diagnostics.Debug.WriteLine("WriteToFile error: " + ex.Message);
            }
        }

        public override Task OnConnected()
        {
            try
            {
                WriteToFile("=== OnConnected ===");
                WriteToFile("ConnectionId: " + Context.ConnectionId);
                int queryStringCount = Context.QueryString != null ? Context.QueryString.Count() : 0;
                WriteToFile("QueryString count: " + queryStringCount);
                // INameValueCollection không có AllKeys, chỉ log giá trị cụ thể
                var customerIdFromQuery = Context.QueryString["customerId"];
                WriteToFile("QueryString[customerId]: " + (customerIdFromQuery ?? "null"));
                
                // Trong SignalR Hub, cần lấy customer từ HttpContext hoặc từ query string
                Customer customer = null;
                
                // Thử lấy từ query string trước (vì đây là cách đáng tin cậy nhất)
                if (Context.QueryString["customerId"] != null)
                {
                    WriteToFile("Found customerId in query string: " + Context.QueryString["customerId"]);
                    if (int.TryParse(Context.QueryString["customerId"], out int customerId))
                    {
                        WriteToFile("Parsed customerId: " + customerId);
                        using (var db = new ecommerceEntities())
                        {
                            customer = db.Customers.Find(customerId);
                            WriteToFile("Customer from DB: " + (customer != null ? customer.CustomerID.ToString() : "null"));
                        }
                    }
                }
                
                // Nếu không lấy được từ query string, thử lấy từ UserManager (có thể không work trong SignalR context)
                if (customer == null)
                {
                    WriteToFile("Trying UserManager...");
                    try
                    {
                        customer = UserManager.CurrentCustomer;
                        WriteToFile("UserManager.CurrentCustomer: " + (customer != null ? customer.CustomerID.ToString() : "null"));
                    }
                    catch (Exception ex)
                    {
                        WriteToFile("UserManager exception: " + ex.Message);
                    }
                }
                
                if (customer != null && customer.CustomerID > 0)
                {
                    var customerId = customer.CustomerID.ToString();
                    _userConnections[Context.ConnectionId] = customerId;
                    _customerConnections[customer.CustomerID] = Context.ConnectionId;
                    
                    System.Diagnostics.Debug.WriteLine("OnConnected: Customer " + customer.CustomerID + " connected with ConnectionId " + Context.ConnectionId);
                    WriteToFile("OnConnected: Customer " + customer.CustomerID + " registered successfully");
                    
                    // Thông báo cho admin có khách hàng mới online
                    Clients.Group("Admins").customerOnline(customer.CustomerID, customer.FullName ?? "Khách hàng");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("OnConnected: No customer found for ConnectionId " + Context.ConnectionId);
                    WriteToFile("OnConnected: No customer found");
                }
            }
            catch (Exception ex)
            {
                // Log error nhưng không throw để không làm gián đoạn connection
                System.Diagnostics.Debug.WriteLine("ChatHub OnConnected error: " + ex.Message);
                WriteToFile("OnConnected error: " + ex.Message);
            }
            
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            try
            {
                if (_userConnections.ContainsKey(Context.ConnectionId))
                {
                    var customerId = _userConnections[Context.ConnectionId];
                    _userConnections.Remove(Context.ConnectionId);
                    
                    if (int.TryParse(customerId, out int id))
                    {
                        _customerConnections.Remove(id);
                        // Thông báo cho admin khách hàng offline
                        Clients.Group("Admins").customerOffline(id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub OnDisconnected error: " + ex.Message);
            }
            
            return base.OnDisconnected(stopCalled);
        }

        /// <summary>
        /// Đăng ký customer với connection (gọi từ client sau khi connect)
        /// </summary>
        public void RegisterCustomer(int customerId)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    var customer = db.Customers.Find(customerId);
                    if (customer != null)
                    {
                        var customerIdStr = customerId.ToString();
                        _userConnections[Context.ConnectionId] = customerIdStr;
                        _customerConnections[customerId] = Context.ConnectionId;
                        
                        System.Diagnostics.Debug.WriteLine("RegisterCustomer: Customer " + customerId + " registered with ConnectionId " + Context.ConnectionId);
                        WriteToFile("RegisterCustomer: Customer " + customerId + " registered with ConnectionId " + Context.ConnectionId);
                        
                        // Thông báo cho admin có khách hàng mới online
                        Clients.Group("Admins").customerOnline(customerId, customer.FullName ?? "Khách hàng");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RegisterCustomer error: " + ex.Message);
                WriteToFile("RegisterCustomer error: " + ex.Message);
            }
        }

        /// <summary>
        /// Test method để kiểm tra SignalR hoạt động
        /// </summary>
        public string TestMethod(string testMessage)
        {
            System.Diagnostics.Debug.WriteLine("TestMethod called with: " + testMessage);
            return "Test response: " + testMessage;
        }

        /// <summary>
        /// Khách hàng gửi tin nhắn
        /// </summary>
        public void SendMessage(string message, int customerId)
        {
            // Log ngay từ đầu - dùng Debug.WriteLine để đảm bảo luôn được ghi
            // Đặt TRƯỚC try-catch để đảm bảo luôn được thực thi
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("SendMessage METHOD ENTRY POINT");
            System.Diagnostics.Debug.WriteLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            System.Diagnostics.Debug.WriteLine("Message parameter: " + (message ?? "null"));
            System.Diagnostics.Debug.WriteLine("CustomerId parameter: " + customerId);
            
            try
            {
                System.Diagnostics.Debug.WriteLine("Context check: " + (Context != null ? "NOT NULL" : "NULL"));
                System.Diagnostics.Debug.WriteLine("ConnectionId: " + (Context?.ConnectionId ?? "null"));
            }
            catch (Exception ctxEx)
            {
                System.Diagnostics.Debug.WriteLine("ERROR accessing Context: " + ctxEx.Message);
            }
            
            System.Diagnostics.Debug.WriteLine("========================================");
            
            // Đảm bảo exception được log vào Debug output
            try
            {
                System.Diagnostics.Debug.WriteLine("Entering try block...");
                // Log ngay từ đầu để đảm bảo method được gọi
                var logMsg = "========== ChatHub.SendMessage CALLED ==========" + Environment.NewLine +
                            "Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + Environment.NewLine +
                            "Message: " + (message ?? "null") + Environment.NewLine +
                            "CustomerId: " + customerId + Environment.NewLine +
                            "ConnectionId: " + (Context?.ConnectionId ?? "null") + Environment.NewLine;
                
                System.Diagnostics.Debug.WriteLine(logMsg);
                
                // Ghi vào file log để đảm bảo không bị mất
                try
                {
                    WriteToFile(logMsg);
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine("WriteToFile failed: " + logEx.Message);
                }
                // Nếu customerId được truyền trực tiếp từ client, dùng nó
                // Nếu không, thử lấy từ connection mapping, query string, hoặc từ database
                Customer customer = null;
                int finalCustomerId = 0;
                
                WriteToFile("SendMessage called with customerId parameter: " + customerId);
                
                // Kiểm tra Context trước khi sử dụng
                if (Context == null)
                {
                    var errorMsg = "ERROR: Context is NULL!";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    WriteToFile(errorMsg);
                    throw new InvalidOperationException("SignalR Context is null. Cannot proceed.");
                }
                
                System.Diagnostics.Debug.WriteLine("Context is valid, ConnectionId: " + Context.ConnectionId);
                
                // Nếu customerId được truyền từ client, dùng nó trực tiếp
                if (customerId > 0)
                {
                    finalCustomerId = customerId;
                    WriteToFile("Using customerId from parameter: " + finalCustomerId);
                    System.Diagnostics.Debug.WriteLine("Looking up customer with ID: " + finalCustomerId);
                    
                    using (var db = new ecommerceEntities())
                    {
                        try
                        {
                            customer = db.Customers.Find(finalCustomerId);
                            WriteToFile("Customer from DB (parameter): " + (customer != null ? customer.CustomerID.ToString() : "null"));
                            System.Diagnostics.Debug.WriteLine("Customer found: " + (customer != null ? "YES" : "NO"));
                            
                            // Lưu vào mapping
                            if (customer != null && !string.IsNullOrEmpty(Context.ConnectionId))
                            {
                                _userConnections[Context.ConnectionId] = finalCustomerId.ToString();
                                _customerConnections[finalCustomerId] = Context.ConnectionId;
                                WriteToFile("Saved to _userConnections mapping");
                            }
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Database error finding customer: " + dbEx.Message);
                            WriteToFile("Database error: " + dbEx.Message);
                            throw;
                        }
                    }
                }
                
                // Nếu không có từ parameter, thử lấy từ connection mapping
                if (customer == null && !string.IsNullOrEmpty(Context.ConnectionId) && _userConnections.ContainsKey(Context.ConnectionId))
                {
                    var customerIdStr = _userConnections[Context.ConnectionId];
                    WriteToFile("Found in _userConnections: " + customerIdStr);
                    if (int.TryParse(customerIdStr, out finalCustomerId))
                    {
                        WriteToFile("Parsed customerId: " + finalCustomerId);
                        using (var db = new ecommerceEntities())
                        {
                            customer = db.Customers.Find(finalCustomerId);
                            WriteToFile("Customer from DB: " + (customer != null ? customer.CustomerID.ToString() : "null"));
                        }
                    }
                }
                
                // Nếu không tìm thấy từ mapping, thử lấy từ query string
                if (customer == null && Context.QueryString != null)
                {
                    WriteToFile("Customer is null, trying query string...");
                    var queryCustomerId = Context.QueryString["customerId"];
                    WriteToFile("QueryString customerId: " + (queryCustomerId ?? "null"));
                    
                    if (queryCustomerId != null && int.TryParse(queryCustomerId, out finalCustomerId))
                    {
                        WriteToFile("Parsed customerId from query string: " + finalCustomerId);
                        using (var db = new ecommerceEntities())
                        {
                            customer = db.Customers.Find(finalCustomerId);
                            WriteToFile("Customer from DB (query string): " + (customer != null ? customer.CustomerID.ToString() : "null"));
                            // Lưu vào mapping để lần sau không cần query lại
                            if (customer != null && !string.IsNullOrEmpty(Context.ConnectionId))
                            {
                                _userConnections[Context.ConnectionId] = finalCustomerId.ToString();
                                _customerConnections[finalCustomerId] = Context.ConnectionId;
                                WriteToFile("Saved to _userConnections mapping");
                            }
                        }
                    }
                }
                
                // Nếu vẫn không tìm thấy, thử lấy từ UserManager (có thể không work trong Hub)
                if (customer == null)
                {
                    WriteToFile("Customer still null, trying UserManager...");
                    try
                    {
                        customer = UserManager.CurrentCustomer;
                        if (customer != null)
                        {
                            finalCustomerId = customer.CustomerID;
                            WriteToFile("Got customer from UserManager: " + finalCustomerId);
                            // Lưu vào mapping
                            if (!string.IsNullOrEmpty(Context.ConnectionId))
                            {
                                _userConnections[Context.ConnectionId] = finalCustomerId.ToString();
                                _customerConnections[finalCustomerId] = Context.ConnectionId;
                                WriteToFile("Saved to _userConnections mapping (UserManager)");
                            }
                        }
                        else
                        {
                            WriteToFile("UserManager.CurrentCustomer returned null");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToFile("UserManager exception: " + ex.Message);
                        System.Diagnostics.Debug.WriteLine("UserManager exception: " + ex.Message);
                    }
                }
                
                // Log tất cả thông tin để debug
                WriteToFile("=== SendMessage Debug Info ===");
                if (Context != null)
                {
                    WriteToFile("ConnectionId: " + (Context.ConnectionId ?? "null"));
                    if (Context.QueryString != null)
                    {
                        int queryStringCount2 = Context.QueryString.Count();
                        WriteToFile("QueryString count: " + queryStringCount2);
                        // INameValueCollection không có AllKeys, chỉ log giá trị cụ thể
                        var customerIdFromQuery = Context.QueryString["customerId"];
                        WriteToFile("QueryString[customerId]: " + (customerIdFromQuery ?? "null"));
                    }
                    if (!string.IsNullOrEmpty(Context.ConnectionId))
                    {
                        WriteToFile("_userConnections contains key: " + _userConnections.ContainsKey(Context.ConnectionId));
                        if (_userConnections.ContainsKey(Context.ConnectionId))
                        {
                            WriteToFile("_userConnections[ConnectionId] = " + _userConnections[Context.ConnectionId]);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("Customer: " + (customer != null ? customer.CustomerID.ToString() : "null"));
                WriteToFile("Final Customer: " + (customer != null ? customer.CustomerID.ToString() : "null"));
                
                if (customer == null || customer.CustomerID <= 0 || string.IsNullOrWhiteSpace(message))
                {
                    var errorMsg = "SendMessage: Invalid customer or message, returning. Customer=" + (customer == null ? "null" : customer.CustomerID.ToString()) + ", Message=" + (string.IsNullOrWhiteSpace(message) ? "empty" : message);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    WriteToFile(errorMsg);
                    Clients.Caller.messageError("Lỗi: Không tìm thấy thông tin khách hàng. Vui lòng refresh trang và đăng nhập lại.");
                    return;
                }

                WriteToFile("=== STEP 1: Creating DbContext ===");
                using (var db = new ecommerceEntities())
                {
                    try
                    {
                        var dbCustomerId = finalCustomerId > 0 ? finalCustomerId : (customer != null ? customer.CustomerID : 0);
                        var messageText = message.Trim();
                        var isFromAdmin = false;
                        var isRead = false;
                        var createdDate = DateTime.Now;
                        
                        WriteToFile("=== STEP 2: Preparing SQL Parameters ===");
                        WriteToFile("dbCustomerId: " + dbCustomerId);
                        WriteToFile("messageText: " + messageText);
                        WriteToFile("isFromAdmin: " + isFromAdmin);
                        WriteToFile("isRead: " + isRead);
                        WriteToFile("createdDate: " + createdDate);
                        
                        // Sử dụng raw SQL để insert vì ChatMessage chưa có trong EDMX
                        // Giống hệt cách SendAdminMessage đã làm (đã hoạt động)
                        var sql = @"
                            INSERT INTO ChatMessage (CustomerID, Message, IsFromAdmin, IsRead, CreatedDate)
                            OUTPUT INSERTED.MessageID
                            VALUES (@p0, @p1, @p2, @p3, @p4);";

                        WriteToFile("=== STEP 3: Executing SQL Query ===");
                        WriteToFile("SQL: " + sql);
                        WriteToFile("Parameters: @p0=" + dbCustomerId + ", @p1=" + messageText + ", @p2=" + isFromAdmin + ", @p3=" + isRead + ", @p4=" + createdDate);
                        
                        System.Diagnostics.Debug.WriteLine("SendMessage: Executing SQL INSERT for CustomerID: " + dbCustomerId);
                        
                        int messageId = 0;
                        try
                        {
                            WriteToFile("=== STEP 4: Calling SqlQuery ===");
                            var result = db.Database.SqlQuery<int>(sql, 
                                dbCustomerId,
                                messageText,
                                isFromAdmin,
                                isRead,
                                createdDate
                            );
                            
                            WriteToFile("=== STEP 5: Getting FirstOrDefault ===");
                            messageId = result.FirstOrDefault();
                            
                            WriteToFile("=== STEP 6: SQL Query Result ===");
                            WriteToFile("MessageID: " + messageId);
                            System.Diagnostics.Debug.WriteLine("SendMessage: Message inserted with ID: " + messageId);
                        }
                        catch (Exception sqlEx)
                        {
                            WriteToFile("=== SQL EXCEPTION ===");
                            WriteToFile("Exception Type: " + sqlEx.GetType().FullName);
                            WriteToFile("Exception Message: " + sqlEx.Message);
                            WriteToFile("Stack Trace: " + sqlEx.StackTrace);
                            if (sqlEx.InnerException != null)
                            {
                                WriteToFile("Inner Exception Type: " + sqlEx.InnerException.GetType().FullName);
                                WriteToFile("Inner Exception Message: " + sqlEx.InnerException.Message);
                                WriteToFile("Inner Stack Trace: " + sqlEx.InnerException.StackTrace);
                            }
                            System.Diagnostics.Debug.WriteLine("SQL Exception: " + sqlEx.Message);
                            throw; // Re-throw để được xử lý bởi catch block bên ngoài
                        }

                        if (messageId <= 0)
                        {
                            var errorMsg = "ERROR: MessageID = 0 after SQL execution!";
                            WriteToFile(errorMsg);
                            System.Diagnostics.Debug.WriteLine(errorMsg);
                            throw new Exception("Không thể lưu tin nhắn vào database. MessageID = 0");
                        }

                        WriteToFile("=== STEP 7: Sending SignalR Messages ===");
                        WriteToFile("Sending to Admins group...");
                        
                        // Gửi tin nhắn cho admin
                        try
                        {
                            Clients.Group("Admins").receiveMessage(customer.CustomerID, customer.FullName ?? "Khách hàng", message, messageId, createdDate);
                            WriteToFile("Message sent to Admins group successfully");
                        }
                        catch (Exception signalrEx)
                        {
                            WriteToFile("Error sending to Admins: " + signalrEx.Message);
                            System.Diagnostics.Debug.WriteLine("SignalR error (Admins): " + signalrEx.Message);
                        }
                        
                        WriteToFile("Sending messageSent to caller...");
                        // Gửi lại cho chính khách hàng để hiển thị
                        try
                        {
                            Clients.Caller.messageSent(messageId, message, createdDate);
                            WriteToFile("Message sent to caller successfully");
                        }
                        catch (Exception signalrEx2)
                        {
                            WriteToFile("Error sending to caller: " + signalrEx2.Message);
                            System.Diagnostics.Debug.WriteLine("SignalR error (Caller): " + signalrEx2.Message);
                        }
                        
                        WriteToFile("=== STEP 8: SendMessage Completed Successfully ===");
                        WriteToFile("Final MessageID: " + messageId);
                    }
                    catch (Exception dbEx)
                    {
                        WriteToFile("=== DATABASE CONTEXT EXCEPTION ===");
                        WriteToFile("Exception Type: " + dbEx.GetType().FullName);
                        WriteToFile("Exception Message: " + dbEx.Message);
                        WriteToFile("Stack Trace: " + dbEx.StackTrace);
                        if (dbEx.InnerException != null)
                        {
                            WriteToFile("Inner Exception Type: " + dbEx.InnerException.GetType().FullName);
                            WriteToFile("Inner Exception Message: " + dbEx.InnerException.Message);
                            WriteToFile("Inner Stack Trace: " + dbEx.InnerException.StackTrace);
                        }
                        System.Diagnostics.Debug.WriteLine("Database Exception: " + dbEx.Message);
                        throw; // Re-throw để được xử lý bởi catch block bên ngoài
                    }
                }
            }
            catch (Exception ex)
            {
                // Log vào Debug output TRƯỚC (luôn hoạt động)
                System.Diagnostics.Debug.WriteLine("========== SENDMESSAGE EXCEPTION CAUGHT ==========");
                System.Diagnostics.Debug.WriteLine("Exception Type: " + ex.GetType().FullName);
                System.Diagnostics.Debug.WriteLine("Exception Message: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("=== INNER EXCEPTION ===");
                    System.Diagnostics.Debug.WriteLine("Inner Type: " + ex.InnerException.GetType().FullName);
                    System.Diagnostics.Debug.WriteLine("Inner Message: " + ex.InnerException.Message);
                    System.Diagnostics.Debug.WriteLine("Inner Stack: " + ex.InnerException.StackTrace);
                }
                
                // Sau đó mới log vào file (có thể fail)
                try
                {
                    WriteToFile("========== SENDMESSAGE EXCEPTION CAUGHT ==========");
                    WriteToFile("Exception Type: " + ex.GetType().FullName);
                    WriteToFile("Exception Message: " + ex.Message);
                    WriteToFile("Stack Trace: " + ex.StackTrace);
                }
                catch { }
                
                var errorMsg = "ChatHub SendMessage error: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(errorMsg);
                System.Diagnostics.Debug.WriteLine("Stack trace: " + ex.StackTrace);
                
                if (ex.InnerException != null)
                {
                    WriteToFile("=== INNER EXCEPTION ===");
                    WriteToFile("Inner Exception Type: " + ex.InnerException.GetType().FullName);
                    WriteToFile("Inner Exception Message: " + ex.InnerException.Message);
                    WriteToFile("Inner Stack Trace: " + ex.InnerException.StackTrace);
                    
                    var innerMsg = "Inner exception: " + ex.InnerException.Message;
                    System.Diagnostics.Debug.WriteLine(innerMsg);
                    System.Diagnostics.Debug.WriteLine("Inner stack trace: " + ex.InnerException.StackTrace);
                    
                    // Log tất cả inner exceptions (có thể có nhiều level)
                    var currentInner = ex.InnerException;
                    int level = 1;
                    while (currentInner != null && level < 5)
                    {
                        WriteToFile("=== INNER EXCEPTION LEVEL " + level + " ===");
                        WriteToFile("Type: " + currentInner.GetType().FullName);
                        WriteToFile("Message: " + currentInner.Message);
                        WriteToFile("Stack Trace: " + currentInner.StackTrace);
                        currentInner = currentInner.InnerException;
                        level++;
                    }
                }
                
                // Gửi error message về client
                try
                {
                    WriteToFile("Attempting to send error message to client...");
                    Clients.Caller.messageError("Lỗi khi gửi tin nhắn: " + ex.Message);
                    WriteToFile("Error message sent to client successfully");
                }
                catch (Exception clientEx)
                {
                    WriteToFile("Failed to send error to client: " + clientEx.Message);
                    System.Diagnostics.Debug.WriteLine("Failed to send error to client: " + clientEx.Message);
                }
                
                WriteToFile("========== END EXCEPTION LOG ==========");
            }
        }

        /// <summary>
        /// Admin gửi tin nhắn trả lời
        /// </summary>
        public void SendAdminMessage(int customerId, string message)
        {
            try
            {
                // Kiểm tra quyền admin (có thể thêm logic kiểm tra ở đây)
                if (customerId <= 0 || string.IsNullOrWhiteSpace(message))
                    return;

                using (var db = new ecommerceEntities())
                {
                    var customer = db.Customers.Find(customerId);
                    if (customer == null)
                        return;

                    System.Diagnostics.Debug.WriteLine("SendAdminMessage: Executing SQL INSERT for CustomerID: " + customerId);
                    
                    // Sử dụng raw SQL để insert vì ChatMessage chưa có trong EDMX
                    var sql = @"
                        INSERT INTO ChatMessage (CustomerID, Message, IsFromAdmin, IsRead, CreatedDate)
                        OUTPUT INSERTED.MessageID
                        VALUES (@p0, @p1, @p2, @p3, @p4);";

                    var messageId = db.Database.SqlQuery<int>(sql, 
                        customerId,
                        message.Trim(),
                        true, // IsFromAdmin
                        false, // IsRead
                        DateTime.Now
                    ).FirstOrDefault();
                    
                    System.Diagnostics.Debug.WriteLine("SendAdminMessage: Message inserted with ID: " + messageId);

                    var createdDate = DateTime.Now;

                    // Gửi tin nhắn cho khách hàng
                    if (_customerConnections.ContainsKey(customerId))
                    {
                        Clients.Client(_customerConnections[customerId]).receiveAdminMessage(message, messageId, createdDate);
                    }
                    
                    // Gửi lại cho admin để hiển thị
                    Clients.Caller.adminMessageSent(customerId, message, messageId, createdDate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub SendAdminMessage error: " + ex.Message);
                Clients.Caller.adminMessageError("Lỗi khi gửi tin nhắn: " + ex.Message);
            }
        }

        /// <summary>
        /// Admin join vào group Admins
        /// </summary>
        public void JoinAdminGroup()
        {
            try
            {
                Groups.Add(Context.ConnectionId, "Admins");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub JoinAdminGroup error: " + ex.Message);
            }
        }

        /// <summary>
        /// Lấy lịch sử chat cho khách hàng
        /// </summary>
        public void GetChatHistory()
        {
            try
            {
                int customerId = 0;
                
                // Ưu tiên 1: Lấy từ _userConnections (đáng tin cậy nhất)
                if (!string.IsNullOrEmpty(Context.ConnectionId) && _userConnections.ContainsKey(Context.ConnectionId))
                {
                    var customerIdStr = _userConnections[Context.ConnectionId];
                    if (!string.IsNullOrEmpty(customerIdStr))
                    {
                        int.TryParse(customerIdStr, out customerId);
                        System.Diagnostics.Debug.WriteLine("GetChatHistory: Found CustomerID " + customerId + " from _userConnections");
                    }
                }
                
                // Ưu tiên 2: Lấy từ query string
                if (customerId <= 0 && Context != null && Context.QueryString != null && Context.QueryString.Count() > 0)
                {
                    var customerIdStr = Context.QueryString["customerId"];
                    if (!string.IsNullOrEmpty(customerIdStr))
                    {
                        int.TryParse(customerIdStr, out customerId);
                        System.Diagnostics.Debug.WriteLine("GetChatHistory: Found CustomerID " + customerId + " from query string");
                    }
                }
                
                // Ưu tiên 3: Lấy từ UserManager
                if (customerId <= 0)
                {
                    var customer = UserManager.CurrentCustomer;
                    if (customer != null)
                    {
                        customerId = customer.CustomerID;
                        System.Diagnostics.Debug.WriteLine("GetChatHistory: Found CustomerID " + customerId + " from UserManager");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("GetChatHistory: Final CustomerID = " + customerId);
                
                if (customerId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("GetChatHistory: CustomerID is 0, returning empty list");
                    Clients.Caller.chatHistory(new List<ChatMessageData>());
                    return;
                }

                using (var db = new ecommerceEntities())
                {
                    try
                    {
                        WriteToFile("=== GetChatHistory DEBUG START ===");
                        WriteToFile("CustomerID: " + customerId);
                        WriteToFile("ConnectionId: " + (Context != null ? Context.ConnectionId : "null"));
                        
                        System.Diagnostics.Debug.WriteLine("=== GetChatHistory DEBUG START ===");
                        System.Diagnostics.Debug.WriteLine("CustomerID: " + customerId);
                        System.Diagnostics.Debug.WriteLine("ConnectionId: " + (Context != null ? Context.ConnectionId : "null"));
                        
                        // Test 1: Kiểm tra xem bảng có tồn tại và có dữ liệu không
                        WriteToFile("=== TEST 1: Check table exists ===");
                        var testTableSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ChatMessage'";
                        var tableExists = db.Database.SqlQuery<int>(testTableSql).FirstOrDefault();
                        WriteToFile("Table ChatMessage exists: " + (tableExists > 0 ? "YES" : "NO"));
                        
                        // Test 2: Đếm tổng số messages
                        WriteToFile("=== TEST 2: Count total messages ===");
                        var countSql = "SELECT COUNT(*) FROM ChatMessage";
                        var totalCount = db.Database.SqlQuery<int>(countSql).FirstOrDefault();
                        WriteToFile("Total messages in ChatMessage table: " + totalCount);
                        
                        // Test 3: Đếm messages của customer này
                        WriteToFile("=== TEST 3: Count messages for CustomerID " + customerId + " ===");
                        var countCustomerSql = "SELECT COUNT(*) FROM ChatMessage WHERE CustomerID = @p0";
                        var customerCount = db.Database.SqlQuery<int>(countCustomerSql, customerId).FirstOrDefault();
                        WriteToFile("Messages for CustomerID " + customerId + ": " + customerCount);
                        
                        // Test 4: Lấy tất cả CustomerID có trong bảng
                        WriteToFile("=== TEST 4: Get all CustomerIDs ===");
                        var allCustomerIdsSql = "SELECT DISTINCT CustomerID FROM ChatMessage";
                        var allCustomerIds = db.Database.SqlQuery<int>(allCustomerIdsSql).ToList();
                        WriteToFile("All CustomerIDs in table: " + string.Join(", ", allCustomerIds));
                        
                        // Test 5: Query đơn giản chỉ lấy MessageID và Message
                        WriteToFile("=== TEST 5: Simple query (MessageID, Message only) ===");
                        var simpleSql = "SELECT MessageID, Message FROM ChatMessage WHERE CustomerID = @p0 ORDER BY CreatedDate ASC";
                        var simpleResults = db.Database.SqlQuery<dynamic>(simpleSql, customerId).ToList();
                        WriteToFile("Simple query result count: " + simpleResults.Count);
                        
                        // Sử dụng raw SQL vì ChatMessage có thể không hoạt động với LINQ trong Database First approach
                        // Giống như cách GetAdminChatHistory và SendMessage đã làm (đã hoạt động)
                        WriteToFile("=== TEST 6: Full query with ChatMessageData ===");
                        var sql = @"
                            SELECT 
                                MessageID,
                                Message,
                                IsFromAdmin,
                                CreatedDate
                            FROM ChatMessage
                            WHERE CustomerID = @p0
                            ORDER BY CreatedDate ASC";

                        WriteToFile("Executing SQL query for CustomerID: " + customerId);
                        System.Diagnostics.Debug.WriteLine("Executing SQL query for CustomerID: " + customerId);
                        
                        List<ChatMessageData> messages = null;
                        try
                        {
                            messages = db.Database.SqlQuery<ChatMessageData>(sql, customerId).ToList();
                            WriteToFile("SqlQuery executed successfully");
                        }
                        catch (Exception sqlEx)
                        {
                            WriteToFile("SqlQuery EXCEPTION: " + sqlEx.Message);
                            WriteToFile("SqlQuery StackTrace: " + sqlEx.StackTrace);
                            if (sqlEx.InnerException != null)
                            {
                                WriteToFile("SqlQuery InnerException: " + sqlEx.InnerException.Message);
                            }
                            messages = new List<ChatMessageData>();
                        }
                        
                        if (messages == null)
                        {
                            WriteToFile("WARNING: messages is NULL, creating empty list");
                            messages = new List<ChatMessageData>();
                        }
                        
                        WriteToFile("After SqlQuery, messages count: " + messages.Count);
                        System.Diagnostics.Debug.WriteLine("After SqlQuery, messages count: " + messages.Count);
                        
                        WriteToFile("SQL Query result count: " + messages.Count);
                        System.Diagnostics.Debug.WriteLine("SQL Query result count: " + messages.Count);
                        
                        if (messages.Count > 0)
                        {
                            WriteToFile("First message: ID=" + messages[0].MessageID + ", Message=" + messages[0].Message);
                            System.Diagnostics.Debug.WriteLine("First message: ID=" + messages[0].MessageID + ", Message=" + messages[0].Message);
                        }
                        else
                        {
                            WriteToFile("WARNING: No messages found for CustomerID: " + customerId);
                            System.Diagnostics.Debug.WriteLine("WARNING: No messages found for CustomerID: " + customerId);
                            
                            // Test query để xem có dữ liệu trong DB không
                            try
                            {
                                var testSql = "SELECT COUNT(*) FROM ChatMessage WHERE CustomerID = @p0";
                                var count = db.Database.SqlQuery<int>(testSql, customerId).FirstOrDefault();
                                WriteToFile("Test query count result: " + count);
                                System.Diagnostics.Debug.WriteLine("Test query count result: " + count);
                            }
                            catch (Exception testEx)
                            {
                                WriteToFile("Test query failed: " + testEx.Message);
                                System.Diagnostics.Debug.WriteLine("Test query failed: " + testEx.Message);
                            }
                        }
                        
                        WriteToFile("=== GetChatHistory DEBUG END ===");
                        System.Diagnostics.Debug.WriteLine("=== GetChatHistory DEBUG END ===");
                        
                        Clients.Caller.chatHistory(messages);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = "GetChatHistory: Exception: " + ex.Message;
                        WriteToFile(errorMsg);
                        WriteToFile("GetChatHistory: StackTrace: " + ex.StackTrace);
                        System.Diagnostics.Debug.WriteLine(errorMsg);
                        System.Diagnostics.Debug.WriteLine("GetChatHistory: StackTrace: " + ex.StackTrace);
                        
                        if (ex.InnerException != null)
                        {
                            WriteToFile("GetChatHistory: InnerException: " + ex.InnerException.Message);
                            WriteToFile("GetChatHistory: InnerException StackTrace: " + ex.InnerException.StackTrace);
                            System.Diagnostics.Debug.WriteLine("GetChatHistory: InnerException: " + ex.InnerException.Message);
                            System.Diagnostics.Debug.WriteLine("GetChatHistory: InnerException StackTrace: " + ex.InnerException.StackTrace);
                        }
                        Clients.Caller.chatHistory(new List<ChatMessageData>());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub GetChatHistory error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("ChatHub GetChatHistory stack trace: " + ex.StackTrace);
                Clients.Caller.chatHistory(new List<ChatMessageData>());
            }
        }

        /// <summary>
        /// Lấy danh sách khách hàng đang chat cho admin
        /// </summary>
        public void GetCustomerList()
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

                    var customers = db.Database.SqlQuery<CustomerChatData>(sql).ToList() ?? new List<CustomerChatData>();
                    
                    // Thêm IsOnline
                    var resultList = new List<object>();
                    foreach (var c in customers)
                    {
                        resultList.Add(new
                        {
                            c.CustomerID,
                            c.FullName,
                            c.Email,
                            c.Phone,
                            c.LastMessage,
                            c.LastMessageDate,
                            c.UnreadCount,
                            IsOnline = _customerConnections.ContainsKey(c.CustomerID)
                        });
                    }
                    var result = resultList;

                    Clients.Caller.customerList(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub GetCustomerList error: " + ex.Message);
                Clients.Caller.customerList(new List<object>());
            }
        }

        /// <summary>
        /// Admin lấy lịch sử chat với một khách hàng
        /// </summary>
        public void GetAdminChatHistory(int customerId)
        {
            try
            {
                if (customerId <= 0)
                {
                    Clients.Caller.adminChatHistory(customerId, new List<ChatMessageData>());
                    return;
                }

                using (var db = new ecommerceEntities())
                {
                    // Sử dụng raw SQL vì ChatMessage chưa có trong EDMX
                    var sql = @"
                        SELECT 
                            MessageID,
                            Message,
                            IsFromAdmin,
                            CreatedDate
                        FROM ChatMessage
                        WHERE CustomerID = @p0
                        ORDER BY CreatedDate ASC";

                    var messages = db.Database.SqlQuery<ChatMessageData>(sql, customerId).ToList() ?? new List<ChatMessageData>();

                    // Đánh dấu đã đọc
                    var updateSql = @"
                        UPDATE ChatMessage 
                        SET IsRead = 1 
                        WHERE CustomerID = @p0 
                        AND IsRead = 0 
                        AND IsFromAdmin = 0";
                    db.Database.ExecuteSqlCommand(updateSql, customerId);

                    Clients.Caller.adminChatHistory(customerId, messages);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub GetAdminChatHistory error: " + ex.Message);
                Clients.Caller.adminChatHistory(customerId, new List<ChatMessageData>());
            }
        }

        /// <summary>
        /// Admin xóa lịch sử chat với một khách hàng
        /// </summary>
        public void DeleteChatHistory(int customerId)
        {
            try
            {
                if (customerId <= 0)
                {
                    Clients.Caller.deleteChatHistoryResult(false, "CustomerID không hợp lệ");
                    return;
                }

                using (var db = new ecommerceEntities())
                {
                    // Xóa tất cả tin nhắn của khách hàng này
                    var deleteSql = @"DELETE FROM ChatMessage WHERE CustomerID = @p0";
                    var rowsAffected = db.Database.ExecuteSqlCommand(deleteSql, customerId);
                    
                    System.Diagnostics.Debug.WriteLine("DeleteChatHistory: Deleted " + rowsAffected + " message(s) for CustomerID: " + customerId);
                    
                    // Thông báo cho admin
                    Clients.Caller.deleteChatHistoryResult(true, "Đã xóa " + rowsAffected + " tin nhắn");
                    
                    // Thông báo cho khách hàng (nếu đang online)
                    if (_customerConnections.ContainsKey(customerId))
                    {
                        Clients.Client(_customerConnections[customerId]).chatHistoryCleared();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatHub DeleteChatHistory error: " + ex.Message);
                Clients.Caller.deleteChatHistoryResult(false, "Lỗi khi xóa lịch sử chat: " + ex.Message);
            }
        }
    }
}


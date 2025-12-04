# Web Bán Linh Kiện Điện Tử

Hệ thống website bán hàng linh kiện điện tử được xây dựng bằng ASP.NET MVC.

## Yêu cầu hệ thống

- **Visual Studio 2019/2022** hoặc cao hơn
- **.NET Framework 4.8**
- **SQL Server 2016** trở lên (hoặc SQL Server Express)
- **IIS Express** (đã đi kèm với Visual Studio)
- **NuGet Package Manager** (đã đi kèm với Visual Studio)

## Hướng dẫn cài đặt

### Bước 1: Clone project

```bash
git clone <repository-url>
cd WebBanLinhKienDienTu
```

### Bước 2: Restore NuGet Packages

1. Mở file `WebBanHang.sln` trong Visual Studio
2. Nhấp chuột phải vào Solution → **Restore NuGet Packages**
3. Hoặc mở Package Manager Console và chạy:
   ```
   nuget restore
   ```

### Bước 3: Tạo Database

1. Mở **SQL Server Management Studio (SSMS)**
2. Kết nối đến SQL Server của bạn
3. Mở file `Database/database.sql`
4. Chạy toàn bộ script để tạo database `WebBanLinhKienMayTinh`
5. Hoặc chạy trực tiếp từ command line:
   ```bash
   sqlcmd -S <server-name> -i Database/database.sql
   ```

### Bước 4: Cấu hình Web.config

Mở file `WebBanHang/Web.config` và cập nhật các thông tin sau:

#### 4.1. Connection String

Tìm và sửa phần `<connectionStrings>`:

```xml
<connectionStrings>
  <!-- Thay DESKTOP-6PB19NI bằng tên SQL Server của bạn -->
  <add name="DefaultConnection" 
       connectionString="Data Source=<YOUR_SQL_SERVER>;Initial Catalog=WebBanLinhKienMayTinh;Integrated Security=True;Trust Server Certificate=True" 
       providerName="System.Data.SqlClient" />
  
  <add name="ecommerceEntities" 
       connectionString="metadata=res://*/Models.Ecommerce.csdl|res://*/Models.Ecommerce.ssdl|res://*/Models.Ecommerce.msl;provider=System.Data.SqlClient;provider connection string=&quot;Data Source=<YOUR_SQL_SERVER>;Initial Catalog=WebBanLinhKienMayTinh;Integrated Security=True;MultipleActiveResultSets=True;application name=EntityFramework&quot;" 
       providerName="System.Data.EntityClient" />
  
  <add name="testmodel" 
       connectionString="metadata=res://*/Models.Model1.csdl|res://*/Models.Model1.ssdl|res://*/Models.Model1.msl;provider=System.Data.SqlClient;provider connection string=&quot;Data Source=<YOUR_SQL_SERVER>;Initial Catalog=WebBanLinhKienMayTinh;Integrated Security=True;MultipleActiveResultSets=True;application name=EntityFramework&quot;" 
       providerName="System.Data.EntityClient" />
</connectionStrings>
```

**Lưu ý:** 
- Thay `<YOUR_SQL_SERVER>` bằng tên SQL Server của bạn (ví dụ: `localhost`, `localhost\SQLEXPRESS`, hoặc tên server cụ thể)
- Nếu dùng SQL Authentication, thay `Integrated Security=True` bằng `User ID=sa;Password=your_password`

#### 4.2. Cấu hình Gemini AI (Tùy chọn)

Tìm phần `<appSettings>` và cập nhật API Key nếu cần:

```xml
<add key="GeminiApiKey" value="YOUR_API_KEY_HERE" />
```

Lấy API key tại: https://aistudio.google.com/app/apikey

#### 4.3. Cấu hình Email (Tùy chọn)

Nếu cần chức năng reset password, cập nhật thông tin SMTP:

```xml
<add key="EmailFrom" value="your-email@gmail.com" />
<add key="SmtpHost" value="smtp.gmail.com" />
<add key="SmtpPort" value="587" />
<add key="SmtpUsername" value="your-email@gmail.com" />
<add key="SmtpPassword" value="your-app-password" />
<add key="SmtpEnableSsl" value="true" />
```

#### 4.4. Cấu hình OAuth (Tùy chọn)

Nếu cần đăng nhập qua Facebook/Google, cập nhật:

```xml
<add key="FbAppId" value="YOUR_FACEBOOK_APP_ID" />
<add key="FbAppSecret" value="YOUR_FACEBOOK_APP_SECRET" />
<add key="GoogleClientId" value="YOUR_GOOGLE_CLIENT_ID" />
<add key="GoogleClientSecret" value="YOUR_GOOGLE_CLIENT_SECRET" />
```

### Bước 5: Build và Chạy project

1. **Build Solution:**
   - Nhấn `Ctrl + Shift + B` hoặc
   - Menu: **Build** → **Build Solution**

2. **Chạy project:**
   - Nhấn `F5` hoặc `Ctrl + F5` để chạy
   - Hoặc nhấp chuột phải vào project → **Set as StartUp Project** → **Debug** → **Start Debugging**

3. **Truy cập ứng dụng:**
   - Website sẽ mở tại: `http://localhost:5001`
   - Port mặc định là 5001 (có thể thay đổi trong Properties của project)

## Cấu trúc thư mục

```
WebBanLinhKienDienTu/
├── Database/
│   └── database.sql          # Script tạo database
├── WebBanHang/
│   ├── Areas/
│   │   └── Admin/            # Khu vực quản trị
│   ├── Controllers/          # Controllers
│   ├── Models/               # Data Models
│   ├── Views/                # Views
│   ├── Core/                 # Core classes
│   ├── Services/             # Business services
│   └── Web.config            # Cấu hình ứng dụng
└── README.md                 # File này
```

## Tính năng chính

- ✅ Quản lý sản phẩm, danh mục
- ✅ Quản lý đơn hàng
- ✅ Giỏ hàng và thanh toán
- ✅ Đăng nhập/Đăng ký
- ✅ Chat hỗ trợ khách hàng (SignalR)
- ✅ Tìm kiếm sản phẩm
- ✅ Quản lý khách hàng
- ✅ Quản lý mã giảm giá
- ✅ Dashboard thống kê

## Tài khoản mặc định

Sau khi tạo database, bạn cần tạo tài khoản admin đầu tiên. Tham khảo file `Database/database.sql` hoặc liên hệ admin để lấy thông tin đăng nhập.

## Xử lý lỗi thường gặp

### Lỗi kết nối database

- Kiểm tra SQL Server đã chạy chưa
- Kiểm tra connection string trong Web.config
- Kiểm tra database đã được tạo chưa

### Lỗi thiếu NuGet packages

```bash
nuget restore
# hoặc
Update-Package -reinstall
```

### Lỗi build

- Xóa thư mục `bin` và `obj`
- Clean Solution (Build → Clean Solution)
- Rebuild Solution (Build → Rebuild Solution)

## Hỗ trợ

Nếu gặp vấn đề trong quá trình cài đặt, vui lòng tạo issue trên repository.

## License


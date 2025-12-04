using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Configuration;

namespace WebBanLinhKienDienTu.Utils
{
    public static class EmailHelper
    {
        public static bool SendPasswordResetEmail(string toEmail, string customerName, string resetLink)
        {
            try
            {
                var fromEmail = ConfigurationManager.AppSettings["EmailFrom"] ?? "noreply@example.com";
                var smtpHost = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                var smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"] ?? "";
                var smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "";
                var enableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");

                var subject = "Đặt lại mật khẩu - Linh Kiện Máy Tính";
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
        <div style='background-color: #D70018; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0;'>
            <h2 style='margin: 0;'>Linh Kiện Máy Tính</h2>
        </div>
        <div style='background-color: white; padding: 30px; border-radius: 0 0 5px 5px;'>
            <h3 style='color: #D70018;'>Xin chào {customerName},</h3>
            <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
            <p>Vui lòng click vào link bên dưới để đặt lại mật khẩu:</p>
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{resetLink}' style='background-color: #D70018; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>Đặt lại mật khẩu</a>
            </div>
            <p style='font-size: 12px; color: #999;'>Hoặc copy và paste link này vào trình duyệt:</p>
            <p style='font-size: 12px; color: #666; word-break: break-all;'>{resetLink}</p>
            <p style='font-size: 12px; color: #999;'>Link này sẽ hết hạn sau 24 giờ.</p>
            <p style='font-size: 12px; color: #999;'>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            <p style='font-size: 12px; color: #999; text-align: center;'>Đây là email tự động, vui lòng không trả lời email này.</p>
        </div>
    </div>
</body>
</html>";

                var message = new MailMessage(fromEmail, toEmail)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.UseDefaultCredentials = false;
                    if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
                    {
                        client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    }
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 30000; // 30 seconds timeout
                    client.Send(message);
                }
                return true;
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine($"Error sending email: {ex.Message}");
                return false;
            }
        }
    }
}


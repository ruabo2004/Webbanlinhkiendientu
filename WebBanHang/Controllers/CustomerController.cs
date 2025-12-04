using Facebook;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Security;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;
using WebBanLinhKienDienTu.Utils;
using WebBanLinhKienDienTu.ViewModels;

namespace WebBanLinhKienDienTu.Controllers
{
    public class CustomerController : BaseController
    {
        //
        // GET: /User/
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [OnlyGuest]
        public ActionResult Register(SignUpViewModel model)
        {
            var existCustomer = Repository.Customer.FindByEmail(model.Email);
            if (existCustomer != null)
            {
                ModelState.AddModelError("Email", "Email đã tồn tại...");
            }
            if (ModelState.IsValid)
            {
                Customer customer = new Customer
                {
                    Email = model.Email,
                    Passwrord = EncryptUtils.MD5(model.Password),
                    FullName = model.FullName,
                    Status = false,
                    RegistrationDate = DateTime.Now
                };
                customer = Repository.Customer.Insert(customer);
                Repository.Customer.SaveChanges();
                SyncLogin(customer, false);
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        [HttpGet]
        [OnlyGuest]
        public ActionResult Register()
        {
            return View();
        }

        [HttpGet]
        [OnlyGuest]
        public ActionResult Login(String ReturnUrl)
        {
            TempData["ReturnUrl"] = ReturnUrl;
            return View(new SignInViewModel());
        }

        [HttpPost]
        [OnlyGuest]
        public ActionResult Login(SignInViewModel model)
        {
            var customer = Repository.Customer.FindByEmail(model.Email);
            if (customer == null)
            {
                ModelState.AddModelError("Email", "Email không tồn tại");
            }
            if (customer != null && !EncryptUtils.PwdCompare(model.Password, customer.Passwrord))
            {
                ModelState.AddModelError("Password", "Mật khẩu không chính xác");
            }
            if (ModelState.IsValid)
            {
                SyncLogin(customer, model.Remember);
                if (TempData["ReturnUrl"] != null && TempData["ReturnUrl"].ToString().Length > 0)
                    return Redirect(TempData["ReturnUrl"].ToString());
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [OnlyGuest]
        public ActionResult ForgetPassword()
        {
            return View(new ForgetPasswordViewModel());
        }

        [HttpPost]
        [OnlyGuest]
        [ValidateAntiForgeryToken]
        public ActionResult ForgetPassword(ForgetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var customer = Repository.Customer.FindByEmail(model.Email);
                if (customer == null)
                {
                    // Không hiển thị lỗi cụ thể để bảo mật, nhưng vẫn hiển thị thông báo thành công
                    TempData["SuccessMessage"] = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu qua email.";
                    return RedirectToAction("Login");
                }

                // Tạo reset token
                var resetToken = Guid.NewGuid().ToString() + DateTime.Now.Ticks.ToString();
                resetToken = EncryptUtils.MD5(resetToken);
                
                // Lưu token vào VerificationCode
                customer.VerificationCode = resetToken;
                Repository.Customer.Update(customer);
                Repository.Customer.SaveChanges();

                // Tạo reset link
                var resetLink = Url.Action("ResetPassword", "Customer", new { token = resetToken }, Request.Url.Scheme);
                
                // Gửi email
                bool emailSent = EmailHelper.SendPasswordResetEmail(customer.Email, customer.FullName ?? "Khách hàng", resetLink);
                
                if (emailSent)
                {
                    TempData["SuccessMessage"] = "Chúng tôi đã gửi link đặt lại mật khẩu đến email của bạn. Vui lòng kiểm tra hộp thư (bao gồm thư mục spam).";
                }
                else
                {
                    // Trong môi trường dev, có thể hiển thị link trực tiếp
                    if (System.Web.HttpContext.Current.IsDebuggingEnabled)
                    {
                        TempData["SuccessMessage"] = $"Link đặt lại mật khẩu: <a href='{resetLink}'>{resetLink}</a>";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Có lỗi xảy ra khi gửi email. Vui lòng thử lại sau hoặc liên hệ với chúng tôi.";
                    }
                }
                
                return RedirectToAction("Login");
            }
            
            return View(model);
        }

        [HttpGet]
        [OnlyGuest]
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Link đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction("Login");
            }

            // Tìm customer với token
            var customer = Repository.Customer.FetchAll()
                .FirstOrDefault(c => c.VerificationCode == token);
            
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction("Login");
            }

            var model = new ResetPasswordViewModel
            {
                Token = token
            };
            
            return View(model);
        }

        [HttpPost]
        [OnlyGuest]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Tìm customer với token
                var customer = Repository.Customer.FetchAll()
                    .FirstOrDefault(c => c.VerificationCode == model.Token);
                
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
                    return RedirectToAction("Login");
                }

                // Cập nhật mật khẩu mới
                customer.Passwrord = EncryptUtils.MD5(model.NewPassword);
                customer.VerificationCode = null; // Xóa token sau khi reset
                Repository.Customer.Update(customer);
                Repository.Customer.SaveChanges();

                TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập với mật khẩu mới.";
                return RedirectToAction("Login");
            }
            
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public ActionResult Profile(String return_url)
        {
            ViewData["Provinces"] = Repository.Province.FetchAll().ToList();
            TempData["return_url"] = return_url;
            
            var currentCustomer = UserManager.CurrentCustomer;
            
            // Load Districts and Wards t? repository thay v lazy loading
            if (currentCustomer.ProvinceID != null)
            {
                var province = Repository.Province.FindById(currentCustomer.ProvinceID.Value);
                if (province != null)
                    ViewData["Districts"] = province.Districts.ToList();
            }
            
            if (currentCustomer.DistrictID != null)
            {
                var district = Repository.District.FindById(currentCustomer.DistrictID.Value);
                if (district != null)
                    ViewData["Wards"] = district.Wards.ToList();
            }
            
            var model = Mapper.Map<Customer, ProfileViewModel>(currentCustomer);
            model.Passwrord = null;
            return View(model);
        }

        [HttpPost]
        public ActionResult Profile(ProfileViewModel model, String return_url)
        {
            ViewData["Provinces"] = Repository.Province.FetchAll().ToList();
            
            var currentCustomer = UserManager.CurrentCustomer;
            
            // Load Districts and Wards t? repository thay v lazy loading
            if (currentCustomer.ProvinceID != null)
            {
                var province = Repository.Province.FindById(currentCustomer.ProvinceID.Value);
                if (province != null)
                    ViewData["Districts"] = province.Districts.ToList();
            }
            
            if (currentCustomer.DistrictID != null)
            {
                var district = Repository.District.FindById(currentCustomer.DistrictID.Value);
                if (district != null)
                    ViewData["Wards"] = district.Wards.ToList();
            }

            if (ModelState.IsValid)
            {
                var customer = Repository.Customer.FindById(UserManager.CurrentCustomer.CustomerID);
                
                // Update basic info
                customer.FullName = model.FullName;
                customer.Phone = model.Phone;
                customer.Address = model.Address;
                customer.ProvinceID = model.ProvinceID;
                customer.DistrictID = model.DistrictID;
                customer.WardID = model.WardID;

                // Handle password change/set - không cần mật khẩu cũ
                bool passwordChanged = false;
                if (!String.IsNullOrEmpty(model.NewPasswrord))
                {
                    // Verify new password confirmation
                    if (model.NewPasswrord != model.ConfirmPasswrord)
                    {
                        ModelState.AddModelError("ConfirmPasswrord", "Mật khẩu xác nhận không khớp");
                        return View(model);
                    }

                    // Update password - không cần kiểm tra mật khẩu cũ
                    customer.Passwrord = EncryptUtils.MD5(model.NewPasswrord);
                    passwordChanged = true;
                }

                Repository.Customer.Update(customer);
                Repository.Customer.SaveChanges();
                
                // Set success message
                if (passwordChanged)
                {
                    TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                    TempData["PasswordChanged"] = true; // Flag để biết đã đổi mật khẩu
                }
                else
                {
                    TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
                }
                
                // Reload lại trang Profile với thông báo
                if (TempData["return_url"] != null)
                {
                    return Redirect(TempData["return_url"].ToString());
                }
                return RedirectToAction("Profile", "Customer");
            }

            return View(model);
        }

        public ActionResult FacebookLogin()
        {
            var fb = new FacebookClient();
            var loginUrl = fb.GetLoginUrl(new
            {
                client_id = ConfigurationManager.AppSettings["FbAppId"],
                client_secret = ConfigurationManager.AppSettings["FbAppSecret"],
                redirect_uri = RedirectUri.AbsoluteUri,
                response_type = "code",
                scope = "email"
            });
            return Redirect(loginUrl.AbsoluteUri);
        }

        public ActionResult FacebookCallback(string code)
        {
            var fb = new FacebookClient();
            dynamic result = fb.Post("oauth/access_token", new
            {
                client_id = ConfigurationManager.AppSettings["FbAppId"],
                client_secret = ConfigurationManager.AppSettings["FbAppSecret"],
                redirect_uri = RedirectUri.AbsoluteUri,
                code = code
            });
            var access_token = result.access_token;
            if (!String.IsNullOrEmpty(access_token))
            {
                fb.AccessToken = access_token;
                dynamic me = fb.Get("me?fields=id,email,name");
                String fbID = me.id;
                fbID = fbID.Trim();
                if (!string.IsNullOrEmpty(fbID))
                {
                    Customer customer = Repository.Customer.FindByFacebookID(fbID);
                    if (customer == null)
                    {
                        customer = new Customer
                        {
                            FacebookID = me.id,
                            Email = me.email,
                            FullName = me.name,
                            Status = true,
                            RegistrationDate = DateTime.Now
                        };
                        customer = Repository.Customer.Insert(customer);
                        Repository.Customer.SaveChanges();
                    }
                    SyncLogin(customer, false);
                }
            }
            return RedirectToAction("Index", "Home");
        }

        private Uri RedirectUri
        {
            get
            {
                var uriBuilder = new UriBuilder(Request.Url);
                uriBuilder.Query = null;
                uriBuilder.Fragment = null;
                uriBuilder.Path = Url.Action("FacebookCallback");
                return uriBuilder.Uri;
            }
        }

        public ActionResult GoogleLogin()
        {
            var clientId = ConfigurationManager.AppSettings["GoogleClientId"];
            var redirectUri = GoogleRedirectUri.AbsoluteUri;
            
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                          $"client_id={clientId}&" +
                          $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                          $"response_type=code&" +
                          $"scope=openid%20email%20profile&" +
                          $"prompt=select_account";  // Force account selection every time
            
            return Redirect(authUrl);
        }

        public async Task<ActionResult> GoogleCallback(string code, string error)
        {
            // Check if Google returned an error
            if (!string.IsNullOrEmpty(error))
            {
                TempData["ErrorMessage"] = $"Đăng nhập Google thất bại: {error}";
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(code))
            {
                TempData["ErrorMessage"] = "Đăng nhập Google thất bại! Không nhận được m xc thật.";
                return RedirectToAction("Login");
            }

            try
            {
                // Enable TLS 1.2 for HTTPS requests
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                var clientId = ConfigurationManager.AppSettings["GoogleClientId"];
                var clientSecret = ConfigurationManager.AppSettings["GoogleClientSecret"];
                var redirectUri = GoogleRedirectUri.AbsoluteUri;

                // Exchange code for token
                using (var client = new HttpClient())
                {
                    var tokenRequest = new FormUrlEncodedContent(new[]
                    {
                        new System.Collections.Generic.KeyValuePair<string, string>("code", code),
                        new System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId),
                        new System.Collections.Generic.KeyValuePair<string, string>("client_secret", clientSecret),
                        new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri", redirectUri),
                        new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "authorization_code")
                    });

                    var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
                    var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                    
                    if (!tokenResponse.IsSuccessStatusCode)
                    {
                        TempData["ErrorMessage"] = $"Đăng nhập Google thất bại! Lỗi: {tokenJson}";
                        return RedirectToAction("Login");
                    }

                    dynamic tokenData = JsonConvert.DeserializeObject(tokenJson);
                    
                    if (tokenData.access_token == null)
                    {
                        TempData["ErrorMessage"] = "Đăng nhập Google thất bại! Không nhận được access token.";
                        return RedirectToAction("Login");
                    }

                    string accessToken = tokenData.access_token;

                    // Get user info
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var userInfoResponse = await client.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                    dynamic userInfo = JsonConvert.DeserializeObject(userInfoResponse);

                    string googleId = userInfo.id?.ToString() ?? "";
                    string email = userInfo.email?.ToString() ?? "";
                    string name = userInfo.name?.ToString() ?? "Google User";

                    // Validate and truncate fields to match database schema
                    if (string.IsNullOrEmpty(googleId))
                    {
                        TempData["ErrorMessage"] = "Đăng nhập Google thất bại! Không nhận được Google ID.";
                        return RedirectToAction("Login");
                    }

                    // Truncate to fit database constraints
                    googleId = googleId.Length > 100 ? googleId.Substring(0, 100) : googleId;
                    email = email.Length > 80 ? email.Substring(0, 80) : email;
                    name = name.Length > 50 ? name.Substring(0, 50) : name;

                    // Find or create customer
                    Customer customer = Repository.Customer.FindByGoogleID(googleId);
                    if (customer == null)
                    {
                        customer = new Customer
                        {
                            GoogleID = googleId,
                            Email = email,
                            FullName = name,
                            Status = true,
                            RegistrationDate = DateTime.Now
                        };
                        customer = Repository.Customer.Insert(customer);
                        Repository.Customer.SaveChanges();
                    }
                    SyncLogin(customer, false);
                }
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                // Get detailed validation errors
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.PropertyName + ": " + x.ErrorMessage);
                
                var fullErrorMessage = string.Join("; ", errorMessages);
                TempData["ErrorMessage"] = $"Validation Error: {fullErrorMessage}";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                // Log detailed error for debugging
                TempData["ErrorMessage"] = $"Đăng nhập Google thất bại! Lỗi: {ex.Message}";
                return RedirectToAction("Login");
            }

            return RedirectToAction("Index", "Home");
        }

        private Uri GoogleRedirectUri
        {
            get
            {
                var uriBuilder = new UriBuilder(Request.Url);
                uriBuilder.Query = null;
                uriBuilder.Fragment = null;
                uriBuilder.Path = Url.Action("GoogleCallback");
                return uriBuilder.Uri;
            }
        }

        private void SyncLogin(Customer userdata, bool remember)
        {
            if (userdata == null) return;
            Response.SetAuthCookie(FormsAuthentication.FormsCookieName, remember, userdata.CustomerID);
        }
    }
}
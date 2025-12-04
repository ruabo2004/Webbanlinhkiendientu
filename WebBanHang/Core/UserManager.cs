using System.Linq;
using System.Web;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core
{
    public class UserManager
    {
        public static Customer CurrentCustomer
        {
            get
            {
                Customer customer = null;
                
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    // The user is authenticated. Return the user from the forms auth ticket.
                    // Kiểm tra xem User có phải là CustomerPrincipal trước khi cast
                    var principal = HttpContext.Current.User as CustomerPrincipal;
                    if (principal != null)
                    {
                        customer = principal.UserData;
                    }
                }
                else if (HttpContext.Current.Items.Contains("User"))
                {
                    // The user is not authenticated, but has successfully logged in.
                    customer = (Customer)HttpContext.Current.Items["User"];
                }
                
                // Eager load MemberRank n?u c� customer
                if (customer != null && customer.CustomerID > 0)
                {
                    // Cache trong HttpContext.Items d? tr�nh query nhi?u l?n
                    var cacheKey = "CurrentCustomerWithRank_" + customer.CustomerID;
                    
                    if (HttpContext.Current.Items.Contains(cacheKey))
                    {
                        return (Customer)HttpContext.Current.Items[cacheKey];
                    }
                    
                    // Load fresh customer with MemberRank from DB using RAW SQL
                    using (var db = new ecommerceEntities())
                    {
                        // Fetch customer data first
                        var customerData = db.Customers
                            .Where(c => c.CustomerID == customer.CustomerID)
                            .FirstOrDefault();
                        
                        if (customerData != null)
                        {
                            MemberRank memberRank = null;
                            
                            // Fetch rank data if exists using RAW SQL to bypass EDMX issues
                            if (customerData.RankID.HasValue)
                            {
                                var rankId = customerData.RankID.Value;
                                
                                // Use raw SQL query
                                var sql = @"SELECT RankID, RankName, RankNameEn, MinSpending, MaxSpending, 
                                           DiscountPercent, IconUrl, ColorCode, Description, Active 
                                           FROM MemberRanks WHERE RankID = @p0";
                                
                                var rankData = db.Database.SqlQuery<MemberRank>(sql, rankId).FirstOrDefault();
                                
                                if (rankData != null)
                                {
                                    memberRank = rankData;
                                }
                            }
                            
                            // Eager load Province, District, Ward
                            Province province = null;
                            District district = null;
                            Ward ward = null;
                            
                            if (customerData.ProvinceID.HasValue)
                            {
                                province = db.Provinces.Find(customerData.ProvinceID.Value);
                            }
                            if (customerData.DistrictID.HasValue)
                            {
                                district = db.Districts.Find(customerData.DistrictID.Value);
                            }
                            if (customerData.WardID.HasValue)
                            {
                                ward = db.Wards.Find(customerData.WardID.Value);
                            }
                            
                            // Create completely detached customer object
                            var detachedCustomer = new Customer
                            {
                                CustomerID = customerData.CustomerID,
                                FullName = customerData.FullName,
                                Email = customerData.Email,
                                Phone = customerData.Phone,
                                Address = customerData.Address,
                                ProvinceID = customerData.ProvinceID,
                                DistrictID = customerData.DistrictID,
                                WardID = customerData.WardID,
                                FacebookID = customerData.FacebookID,
                                GoogleID = customerData.GoogleID,
                                Status = customerData.Status,
                                RegistrationDate = customerData.RegistrationDate,
                                RankID = customerData.RankID,
                                TotalSpent = customerData.TotalSpent,
                                Passwrord = customerData.Passwrord,
                                MemberRank = memberRank,
                                Province = province,
                                District = district,
                                Ward = ward
                            };
                            
                            // Cache l?i
                            HttpContext.Current.Items[cacheKey] = detachedCustomer;
                            return detachedCustomer;
                        }
                    }
                }
                
                return customer;
            }
        }

        public static User CurrentUser
        {
            get
            {
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    // The user is authenticated. Return the user from the forms auth ticket.
                    // Kiểm tra xem User có phải là UserPrincipal trước khi cast
                    var principal = HttpContext.Current.User as UserPrincipal;
                    if (principal != null)
                    {
                        return principal.UserData;
                    }
                }
                
                if (HttpContext.Current.Items.Contains("User"))
                {
                    // The user is not authenticated, but has successfully logged in.
                    return (User)HttpContext.Current.Items["User"];
                }
                
                return null;
            }
        }
    }
}
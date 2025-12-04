using System;
using System.Data.Entity;
using System.Linq;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class CustomerRepository : RepositoryModel<Customer>
    {
        public CustomerRepository(DbContext db)
            : base(db)
        {
        }

        public Customer FindByEmail(String email)
        {
            return FetchAll().Where(item => item.Email.Equals(email)).FirstOrDefault();
        }

        public Customer FindByFacebookID(String fbID)
        {
            return FetchAll().Where(item => item.FacebookID.Equals(fbID)).FirstOrDefault();
        }

        public Customer FindByGoogleID(String googleID)
        {
            return FetchAll().Where(item => item.GoogleID.Equals(googleID)).FirstOrDefault();
        }
    }
}
using System;
using System.Data.Entity;

namespace WebBanLinhKienDienTu.Core
{
    public class WebDbContext : DbContext
    {
        //public WebDbContext() : base("DefaultConnection")
        //{
        //}
        public WebDbContext(String connectionString) : base(connectionString)
        {
        }
    }
}
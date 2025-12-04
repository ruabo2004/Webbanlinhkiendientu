using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class CouponRepository : RepositoryModel<Coupon>
    {
        public CouponRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}


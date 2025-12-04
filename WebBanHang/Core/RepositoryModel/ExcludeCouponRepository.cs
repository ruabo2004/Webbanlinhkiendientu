using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class ExcludeCouponRepository : RepositoryModel<ExcludeCoupon>
    {
        public ExcludeCouponRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}


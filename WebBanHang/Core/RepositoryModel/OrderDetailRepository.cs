using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class OrderDetailRepository : RepositoryModel<OrderDetail>
    {
        public OrderDetailRepository(DbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
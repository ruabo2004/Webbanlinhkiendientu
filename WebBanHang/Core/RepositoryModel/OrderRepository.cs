using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class OrderRepository : RepositoryModel<Order>
    {
        public OrderRepository(DbContext db)
            : base(db)
        {
        }
    }
}
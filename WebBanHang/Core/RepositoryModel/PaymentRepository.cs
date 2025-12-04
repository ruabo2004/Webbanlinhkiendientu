using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class PaymentRepository : RepositoryModel<Payment>
    {
        public PaymentRepository(DbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class WardRepository : RepositoryModel<Ward>
    {
        public WardRepository(DbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
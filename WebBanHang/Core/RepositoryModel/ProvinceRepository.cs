using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class ProvinceRepository : RepositoryModel<Province>
    {
        public ProvinceRepository(DbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class DistrictRepository : RepositoryModel<District>
    {
        public DistrictRepository(DbContext dbContext)
            : base(dbContext)
        {
        }
    }
}
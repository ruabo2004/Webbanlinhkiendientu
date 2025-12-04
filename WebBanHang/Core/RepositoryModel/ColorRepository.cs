using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class ColorRepository : RepositoryModel<Color>
    {
        public ColorRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
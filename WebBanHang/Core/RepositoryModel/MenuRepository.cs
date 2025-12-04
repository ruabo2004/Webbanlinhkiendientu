using System.Data.Entity;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class MenuRepository : RepositoryModel<Menu>
    {
        public MenuRepository(DbContext db) : base(db)
        {
        }
    }
}
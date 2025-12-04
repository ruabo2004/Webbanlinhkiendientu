using System.Linq;

namespace WebBanLinhKienDienTu.Core
{
    public interface IRepository<T> where T : class
    {
        T Insert(T entity);

        bool Delete(params object[] key);

        T Update(T entity);

        IQueryable<T> FetchAll();

        T FindById(params object[] key);

        int SaveChanges();
    }
}
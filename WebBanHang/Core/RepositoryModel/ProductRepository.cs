using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class ProductRepository : RepositoryModel<Product>
    {
        public ProductRepository(DbContext dbContext)
            : base(dbContext)
        {
        }

        public List<Product> GetNewProduct(int number)
        {
            return FetchAll().Where(item => item.Active == true)
                .OrderByDescending(item => item.CreateDate)
                .Take(number)
                .ToList();
        }

        public IEnumerable<Product> GetProductInGroup(int group)
        {
            return FetchAll().Where(item => item.GroupID == group).ToList();
        }

        public IEnumerable<Product> BestProductSale()
        {
            // Get products that have sale price (SalePrice > 0 and less than Price)
            return FetchAll()
                .Where(p => p.Active && p.SalePrice > 0 && p.SalePrice < p.Price && p.Stock > 0)
                .OrderByDescending(p => ((p.Price - p.SalePrice) * 100) / p.Price) // Order by discount percentage
                .Take(10)
                .ToList();
        }
    }
}
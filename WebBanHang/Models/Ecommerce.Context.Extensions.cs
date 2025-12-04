//------------------------------------------------------------------------------
// Extension for ecommerceEntities
// Manual changes - won't be overwritten
//------------------------------------------------------------------------------

namespace WebBanLinhKienDienTu.Models
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.Validation;
    using System.Linq;
    using System.Text;
    using WebBanLinhKienDienTu.Services;
    
    public partial class ecommerceEntities : DbContext
    {
        /// <summary>
        /// Override SaveChanges để tự động trigger ETL khi có thay đổi
        /// </summary>
        public override int SaveChanges()
        {
            // Lấy danh sách entities bị thay đổi trước khi save
            var changedProducts = ChangeTracker.Entries<Product>()
                .Where(e => e.State == EntityState.Added || 
                           e.State == EntityState.Modified || 
                           e.State == EntityState.Deleted)
                .Select(e => e.Entity.ProductID)
                .ToList();
            
            try
            {
                // Gọi SaveChanges gốc
                var result = base.SaveChanges();
                
                // Trigger ETL để update index (async, không block)
                if (changedProducts.Any())
                {
                    System.Threading.Tasks.Task.Run(() => 
                    {
                        try
                        {
                            using (var indexService = new ProductIndexService())
                            {
                                foreach (var productId in changedProducts)
                                {
                                    indexService.UpdateProductIndex(productId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"ETL Hook Error: {ex.Message}");
                        }
                    });
                }
                
                return result;
            }
            catch (DbEntityValidationException ex)
            {
                // Log chi tiết lỗi validation
                var errorMessages = new StringBuilder();
                errorMessages.AppendLine("Validation failed for one or more entities:");
                
                foreach (var validationError in ex.EntityValidationErrors)
                {
                    var entityName = validationError.Entry.Entity.GetType().Name;
                    errorMessages.AppendLine($"Entity: {entityName}");
                    
                    foreach (var error in validationError.ValidationErrors)
                    {
                        errorMessages.AppendLine($"  Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                    }
                }
                
                var fullErrorMessage = errorMessages.ToString();
                System.Diagnostics.Debug.WriteLine(fullErrorMessage);
                
                // Re-throw với thông tin chi tiết hơn
                throw new DbEntityValidationException(
                    $"Validation failed. Details:\n{fullErrorMessage}", 
                    ex.EntityValidationErrors);
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Services
{
    /// <summary>
    /// Service để tìm kiếm và index sản phẩm cho AI
    /// Sử dụng SQL Server Full-Text Search
    /// </summary>
    public class ProductIndexService : IDisposable
    {
        private readonly ecommerceEntities _db;
        private bool _disposed = false;

        public ProductIndexService()
        {
            _db = new ecommerceEntities();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _db?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Tìm kiếm sản phẩm liên quan dựa trên câu hỏi
        /// ✅ SỬ DỤNG CÙNG LOGIC VỚI THANH TÌM KIẾM (ProductController.Search)
        /// Logic đơn giản: Tìm trong ProductName.Contains(query)
        /// </summary>
        public List<ProductSearchResult> SearchProducts(string query, int maxResults = 5)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 [ProductIndexService] SearchProducts: query='{query}', maxResults={maxResults}");
                
                using (var db = new ecommerceEntities())
                {
                    // ✅ ĐƠN GIẢN HÓA: Dùng cùng logic với thanh tìm kiếm
                    // Logic thanh tìm kiếm: products.Where(p => p.ProductName.ToLower().Contains(searchTerm))
                    var searchTerm = query.ToLower().Trim();
                    
                    // ✅ Tìm sản phẩm trực tiếp theo tên (giống thanh tìm kiếm)
                    var matchingProducts = db.Products
                        .Where(p => p.Active == true && 
                                   p.ProductName != null &&
                                   p.ProductName.ToLower().Contains(searchTerm))
                            .Select(p => new
                            {
                                ProductID = p.ProductID,
                            ProductName = p.ProductName,
                            Detail = p.Detail,
                                Price = p.Price,
                            SalePrice = p.SalePrice,
                            Stock = p.Stock,
                            GroupName = p.GroupProduct != null ? p.GroupProduct.GroupName : null
                        })
                        .OrderByDescending(p => p.ProductName.ToLower().Equals(searchTerm) ? 1 : 0) // Ưu tiên exact match
                        .ThenBy(p => p.ProductName.ToLower().IndexOf(searchTerm)) // Ưu tiên match ở đầu
                        .ThenBy(p => p.ProductName.Length) // Ưu tiên tên ngắn hơn
                        .Take(maxResults)
                            .ToList();

                    System.Diagnostics.Debug.WriteLine($"✅ [ProductIndexService] Tìm thấy {matchingProducts.Count} sản phẩm khớp với '{query}' (giống logic thanh tìm kiếm)");
                    
                    if (matchingProducts.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ [ProductIndexService] Không tìm thấy sản phẩm nào khớp với '{query}'");
                        return new List<ProductSearchResult>();
                    }
                    
                    // ✅ Load hình ảnh cho các sản phẩm tìm được
                    var productIds = matchingProducts.Select(p => p.ProductID).ToList();
                    var allImages = new Dictionary<int, string>();
                    
                    foreach (var productId in productIds)
                    {
                        var productImage = db.ImageProducts
                            .Where(img => img.ProductID == productId && 
                                         img.ImagePath != null && 
                                         !string.IsNullOrEmpty(img.ImagePath))
                                    .OrderBy(img => img.ImageID)
                                    .Select(img => img.ImagePath)
                            .FirstOrDefault();
                        
                        if (!string.IsNullOrEmpty(productImage))
                        {
                            allImages[productId] = productImage;
                        }
                    }
                    
                    // ✅ Tạo kết quả tìm kiếm
                    var results = matchingProducts.Select(p => {
                        var correctImage = allImages.ContainsKey(p.ProductID) ? allImages[p.ProductID] : null;
                        
                        System.Diagnostics.Debug.WriteLine($"   ✅ ProductID: {p.ProductID}, Name: {p.ProductName}, ImagePath: {(string.IsNullOrEmpty(correctImage) ? "NULL" : correctImage)}");
                        
                        return new ProductSearchResult
                            {
                                ProductID = p.ProductID,
                                Name = p.ProductName,
                                Description = p.Detail,
                                Price = p.Price,
                            PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                                CategoryName = p.GroupName,
                                TotalQuantity = p.Stock,
                            ImagePath = correctImage,
                            RelevanceScore = p.ProductName.ToLower().Equals(searchTerm) ? 1.0 : 0.9 // Exact match = 1.0, contains = 0.9
                        };
                    }).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"✅ [ProductIndexService] Trả về {results.Count} sản phẩm");
                    return results;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [ProductIndexService] SearchProducts Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                return new List<ProductSearchResult>();
            }
        }

        /// <summary>
        /// Lấy sản phẩm giá trên X gần nhất (gần nhất với X nhưng vẫn > X)
        /// </summary>
        public List<ProductSearchResult> GetProductsAbovePrice(decimal price, int maxResults = 5)
        {
            return GetProductsAbovePriceWithCategory(price, null, maxResults);
        }
        
        /// <summary>
        /// Lấy sản phẩm giá trên X trong category cụ thể
        /// </summary>
        public List<ProductSearchResult> GetProductsAbovePriceWithCategory(decimal price, string category, int maxResults = 5)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"💰 [ProductIndexService] GetProductsAbovePriceWithCategory({price:N0}đ, {category ?? "null"})");
                
                using (var db = new ecommerceEntities())
                {
                    var allImages = db.ImageProducts
                        .Where(img => img.ImagePath != null && !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(g => g.Key, g => g.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault());
                    
                    var query = db.Products.Where(p => p.Active == true && p.Stock > 0 && p.Price > price);
                    
                    if (!string.IsNullOrEmpty(category))
                        query = query.Where(p => p.GroupProduct != null && p.GroupProduct.GroupName.Contains(category));
                    
                    var products = query.OrderBy(p => p.Price).Take(maxResults).ToList()
                        .Select(p => new ProductSearchResult
                        {
                            ProductID = p.ProductID,
                            Name = p.ProductName,
                            Description = p.Detail,
                            Price = p.Price,
                            PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                            CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                            TotalQuantity = p.Stock,
                            ImagePath = allImages.ContainsKey(p.ProductID) ? allImages[p.ProductID] : p.ImageProducts.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault(),
                            RelevanceScore = 1.0
                        }).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"💰 [ProductIndexService] GetProductsAbovePriceWithCategory({price:N0}đ, {category ?? "null"}): {products.Count} sản phẩm");
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProductsAbovePriceWithCategory Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }

        /// <summary>
        /// Lấy sản phẩm giá dưới X gần nhất (gần nhất với X nhưng vẫn < X)
        /// </summary>
        public List<ProductSearchResult> GetProductsBelowPrice(decimal price, int maxResults = 5)
        {
            return GetProductsBelowPriceWithCategory(price, null, maxResults);
        }
        
        /// <summary>
        /// Lấy sản phẩm giá dưới X trong category cụ thể
        /// </summary>
        public List<ProductSearchResult> GetProductsBelowPriceWithCategory(decimal price, string category, int maxResults = 5)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    var allImages = db.ImageProducts
                        .Where(img => img.ImagePath != null && !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(g => g.Key, g => g.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault());
                    
                    var query = db.Products.Where(p => p.Active == true && p.Stock > 0 && p.Price < price && p.Price > 0);
                    
                    if (!string.IsNullOrEmpty(category))
                        query = query.Where(p => p.GroupProduct != null && p.GroupProduct.GroupName.Contains(category));
                    
                    var products = query.OrderByDescending(p => p.Price).Take(maxResults).ToList()
                        .Select(p => new ProductSearchResult
                        {
                            ProductID = p.ProductID,
                            Name = p.ProductName,
                            Description = p.Detail,
                            Price = p.Price,
                            PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                            CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                            TotalQuantity = p.Stock,
                            ImagePath = allImages.ContainsKey(p.ProductID) ? allImages[p.ProductID] : p.ImageProducts.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault(),
                            RelevanceScore = 1.0
                        }).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"💰 [ProductIndexService] GetProductsBelowPriceWithCategory({price:N0}đ, {category ?? "null"}): {products.Count} sản phẩm");
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProductsBelowPriceWithCategory Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }
        
        /// <summary>
        /// Lấy sản phẩm trong khoảng giá X-Y (gần nhất với khoảng)
        /// </summary>
        public List<ProductSearchResult> GetProductsInPriceRange(decimal minPrice, decimal maxPrice, int maxResults = 5)
        {
            return GetProductsInPriceRangeWithCategory(minPrice, maxPrice, null, maxResults);
        }
        
        /// <summary>
        /// Lấy sản phẩm trong khoảng giá X-Y trong category cụ thể
        /// </summary>
        public List<ProductSearchResult> GetProductsInPriceRangeWithCategory(decimal minPrice, decimal maxPrice, string category, int maxResults = 5)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    var allImages = db.ImageProducts
                        .Where(img => img.ImagePath != null && !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(g => g.Key, g => g.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault());
                    
                    decimal centerPrice = (minPrice + maxPrice) / 2;
                    
                    var query = db.Products.Where(p => p.Active == true && p.Stock > 0 && p.Price >= minPrice && p.Price <= maxPrice);
                    
                    if (!string.IsNullOrEmpty(category))
                        query = query.Where(p => p.GroupProduct != null && p.GroupProduct.GroupName.Contains(category));
                    
                    var products = query
                        .OrderBy(p => Math.Abs(p.Price - centerPrice))
                        .ThenBy(p => p.Price)
                        .Take(maxResults)
                        .ToList()
                        .Select(p => new ProductSearchResult
                        {
                            ProductID = p.ProductID,
                            Name = p.ProductName,
                            Description = p.Detail,
                            Price = p.Price,
                            PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                            CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                            TotalQuantity = p.Stock,
                            ImagePath = allImages.ContainsKey(p.ProductID) ? allImages[p.ProductID] : p.ImageProducts.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault(),
                            RelevanceScore = 1.0
                        }).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"💰 [ProductIndexService] GetProductsInPriceRangeWithCategory({minPrice:N0}đ - {maxPrice:N0}đ, {category ?? "null"}): {products.Count} sản phẩm");
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProductsInPriceRangeWithCategory Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }
        
        /// <summary>
        /// Tìm kiếm theo khoảng giá (OLD - giữ lại để tương thích)
        /// </summary>
        public List<ProductSearchResult> SearchByPriceRange(decimal minPrice, decimal maxPrice, string category = null, int maxResults = 5)
        {
            // ✅ Delegate sang GetProductsInPriceRange để tái sử dụng code
            return GetProductsInPriceRange(minPrice, maxPrice, maxResults);
        }
        
        /// <summary>
        /// Lấy sản phẩm giá rẻ nhất
        /// </summary>
        public List<ProductSearchResult> GetCheapestProducts(int maxResults = 5)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    // ✅ Load tất cả hình ảnh về memory trước
                    var allImages = db.ImageProducts
                        .Where(img => img.ImagePath != null && !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderBy(img => img.ImageID)
                                  .Select(img => img.ImagePath)
                                  .FirstOrDefault()
                        );
                    
                    // Lấy sản phẩm giá rẻ nhất (ưu tiên PromotionPrice nếu có)
                    var products = db.Products
                        .Where(p => p.Active == true && p.Stock > 0 && p.Price > 0)
                        .OrderBy(p => p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price) // Ưu tiên giá khuyến mãi
                        .Take(maxResults)
                        .ToList()
                        .Select(p => {
                            var correctImage = allImages.ContainsKey(p.ProductID) 
                                ? allImages[p.ProductID] 
                                : p.ImageProducts
                                    .OrderBy(img => img.ImageID)
                                    .Select(img => img.ImagePath)
                                    .FirstOrDefault();
                            
                            return new ProductSearchResult
                            {
                                ProductID = p.ProductID,
                                Name = p.ProductName,
                                Description = p.Detail,
                                Price = p.Price,
                                PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                                CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                                TotalQuantity = p.Stock,
                                ImagePath = correctImage, // ✅ Dùng hình ảnh đúng từ dictionary
                                RelevanceScore = 1.0
                            };
                        })
                        .ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"💰 [ProductIndexService] GetCheapestProducts: Tìm thấy {products.Count} sản phẩm");
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCheapestProducts Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }
        
        /// <summary>
        /// Lấy sản phẩm giá đắt nhất
        /// </summary>
        public List<ProductSearchResult> GetMostExpensiveProducts(int maxResults = 5)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    // ✅ Load tất cả hình ảnh về memory trước
                    var allImages = db.ImageProducts
                        .Where(img => img.ImagePath != null && !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderBy(img => img.ImageID)
                                  .Select(img => img.ImagePath)
                                  .FirstOrDefault()
                        );
                    
                    // Lấy sản phẩm giá đắt nhất (theo giá gốc, không phải giá khuyến mãi)
                    var products = db.Products
                        .Where(p => p.Active == true && p.Stock > 0 && p.Price > 0)
                        .OrderByDescending(p => p.Price) // Sắp xếp theo giá gốc giảm dần
                        .Take(maxResults)
                        .ToList()
                        .Select(p => {
                            var correctImage = allImages.ContainsKey(p.ProductID) 
                                ? allImages[p.ProductID] 
                                : p.ImageProducts
                                .OrderBy(img => img.ImageID)
                                .Select(img => img.ImagePath)
                                    .FirstOrDefault();
                            
                            return new ProductSearchResult
                            {
                                ProductID = p.ProductID,
                                Name = p.ProductName,
                                Description = p.Detail,
                                Price = p.Price,
                                PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                                CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                                TotalQuantity = p.Stock,
                                ImagePath = correctImage, // ✅ Dùng hình ảnh đúng từ dictionary
                            RelevanceScore = 1.0
                            };
                        })
                        .ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"💎 [ProductIndexService] GetMostExpensiveProducts: Tìm thấy {products.Count} sản phẩm");
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMostExpensiveProducts Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }

        /// <summary>
        /// Lấy top sản phẩm theo category (random hoặc popular)
        /// </summary>
        public List<ProductSearchResult> GetTopProductsByCategory(string category, int maxResults = 5)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    // ✅ Load tất cả hình ảnh về memory trước
                    var allImages = db.ImageProducts
                        .Where(img => img.ImagePath != null && !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderBy(img => img.ImageID)
                                  .Select(img => img.ImagePath)
                                  .FirstOrDefault()
                        );
                    
                    var query = db.Products
                        .Where(p => p.Active == true && p.Stock > 0 && p.Price > 0); // Chỉ lấy sản phẩm còn hàng và có giá

                    // Filter by category if provided
                    if (!string.IsNullOrEmpty(category))
                    {
                        query = query.Where(p => p.GroupProduct != null && p.GroupProduct.GroupName.Contains(category));
                    }

                    // ✅ Sắp xếp theo tiêu chí: Khuyến mãi > Giá > Ngẫu nhiên
                    var products = query
                        .OrderByDescending(p => p.SalePrice > 0 && p.SalePrice < p.Price ? 1 : 0) // Ưu tiên có khuyến mãi
                        .ThenBy(p => p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price) // Giá tăng dần
                        .ThenByDescending(p => p.Stock) // Stock cao hơn
                        .Take(maxResults * 2) // Lấy nhiều hơn để có nhiều lựa chọn
                        .ToList()
                        .Select(p => {
                            // ✅ Lấy đúng hình ảnh từ dictionary
                            var correctImage = allImages.ContainsKey(p.ProductID) 
                                ? allImages[p.ProductID] 
                                : p.ImageProducts
                                    .OrderBy(img => img.ImageID)
                                    .Select(img => img.ImagePath)
                                    .FirstOrDefault();
                            
                            return new ProductSearchResult
                        {
                            ProductID = p.ProductID,
                            Name = p.ProductName,
                            Description = p.Detail,
                            Price = p.Price,
                                PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                                CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                                TotalQuantity = p.Stock,
                                ImagePath = correctImage, // ✅ Dùng hình ảnh đúng từ dictionary
                            RelevanceScore = 1.0
                            };
                        })
                        .Where(p => !string.IsNullOrEmpty(p.ImagePath)) // Chỉ lấy sản phẩm có hình ảnh
                        .Take(maxResults)
                        .ToList();
                    
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTopProductsByCategory Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }

        /// <summary>
        /// Lấy context cho AI từ kết quả tìm kiếm
        /// </summary>
        public string BuildContextFromProducts(List<ProductSearchResult> products)
        {
            if (products == null || products.Count == 0)
            {
                return "Không có sản phẩm phù hợp.";
            }

            // ✅ Giới hạn tối đa 5 sản phẩm để giảm token
            var limitedProducts = products.Take(5).ToList();
            
            var sb = new StringBuilder();
            sb.AppendLine("=== SẢN PHẨM ===");

            foreach (var product in limitedProducts)
            {
                // ✅ Rút ngắn format để giảm token
                sb.Append($"{product.Name}");
                
                if (product.PromotionPrice.HasValue && product.PromotionPrice < product.Price)
                {
                    sb.AppendLine($" - {product.PromotionPrice:N0}đ (KM từ {product.Price:N0}đ)");
                }
                else
                {
                    sb.AppendLine($" - {product.Price:N0}đ");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Tính điểm relevance dựa trên độ khớp tên sản phẩm với query
        /// Điểm cao hơn = khớp tốt hơn
        /// </summary>
        private double CalculateNameRelevance(string productName, string query)
        {
            if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(query))
                return 0.0;
            
            var productNameLower = productName.ToLower();
            var queryLower = query.ToLower();
            
            // Khớp chính xác = điểm cao nhất (1.0)
            if (productNameLower.Equals(queryLower))
                return 1.0;
            
            // Bắt đầu bằng query = điểm cao (0.9)
            if (productNameLower.StartsWith(queryLower))
                return 0.9;
            
            // Chứa query = điểm trung bình (0.7)
            if (productNameLower.Contains(queryLower))
            {
                // Query càng dài so với tên sản phẩm, điểm càng cao
                var ratio = (double)query.Length / productName.Length;
                return 0.5 + (ratio * 0.4); // 0.5 - 0.9
            }
            
            // Tìm kiếm theo từng từ trong query
            var queryWords = queryLower.Split(new[] { ' ', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            var matchedWords = queryWords.Count(w => productNameLower.Contains(w));
            
            if (matchedWords > 0)
            {
                var matchRatio = (double)matchedWords / queryWords.Length;
                return 0.3 + (matchRatio * 0.4); // 0.3 - 0.7
            }
            
            return 0.1; // Điểm thấp nhất
        }

        /// <summary>
        /// Trích xuất keywords từ câu hỏi
        /// </summary>
        private List<string> ExtractKeywords(string query)
        {
            // Remove common Vietnamese stop words
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "là", "của", "và", "có", "được", "một", "này", "đó", "cho", "với",
                "tôi", "bạn", "mình", "cái", "con", "chiếc", "em", "anh",
                "muốn", "cần", "tìm", "kiếm", "xem", "giúp", "gì", "nào", "đâu", "có",
                "the", "a", "an", "is", "are", "was", "were", "be", "have", "has"
            };

            var words = query.ToLower()
                .Split(new[] { ' ', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .ToList();
            
            // ✅ Fix: Nếu không có keyword nào (tất cả đều bị loại bỏ), thử lấy từ dài nhất
            if (words.Count == 0)
            {
                var allWords = query.ToLower()
                    .Split(new[] { ' ', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                    .OrderByDescending(w => w.Length)
                    .Take(3)
                    .ToList();
                
                if (allWords.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [ProductIndexService] Tất cả keywords bị loại bỏ, dùng fallback: {string.Join(", ", allWords)}");
                    return allWords;
                }
            }

            return words;
        }

        /// <summary>
        /// Update index khi có thay đổi sản phẩm (gọi từ ETL hook)
        /// </summary>
        public void UpdateProductIndex(int productId)
        {
            // Placeholder cho future enhancement
            // Có thể thêm cache invalidation, update full-text index, etc.
            System.Diagnostics.Debug.WriteLine($"Product {productId} index updated");
        }

        /// <summary>
        /// Lấy danh sách categories có sẵn trong shop
        /// </summary>
        public List<CategoryInfo> GetAvailableCategories()
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    var categories = db.Products
                        .Where(p => p.Active == true && p.GroupProduct != null)
                        .GroupBy(p => p.GroupProduct)
                        .Select(g => new
                        {
                            GroupProduct = g.Key,
                            ProductCount = g.Count(),
                            MinPrice = g.Min(p => p.Price),
                            MaxPrice = g.Max(p => p.Price),
                            AvgPrice = g.Average(p => p.Price)
                        })
                        .ToList()
                        .Where(x => x.ProductCount > 0)
                        .Select(x => new CategoryInfo
                        {
                            CategoryName = x.GroupProduct.GroupName,
                            ProductCount = x.ProductCount,
                            MinPrice = x.MinPrice,
                            MaxPrice = x.MaxPrice,
                            AvgPrice = (decimal)x.AvgPrice
                        })
                        .OrderByDescending(c => c.ProductCount)
                        .ToList();

                    return categories;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAvailableCategories Error: {ex.Message}");
                return new List<CategoryInfo>();
            }
        }

        /// <summary>
        /// Lấy thông tin giá của một category cụ thể
        /// </summary>
        public CategoryPriceInfo GetCategoryPriceInfo(string categoryName)
        {
            try
            {
                using (var db = new ecommerceEntities())
                {
                    var products = db.Products
                        .Where(p => p.Active == true && p.GroupProduct.GroupName == categoryName)
                        .Select(p => new
                        {
                            p.Price
                        })
                        .ToList();

                    if (!products.Any())
                        return null;

                    var prices = products.Select(p => p.Price).ToList();

                    return new CategoryPriceInfo
                    {
                        CategoryName = categoryName,
                        MinPrice = prices.Min(),
                        MaxPrice = prices.Max(),
                        AvgPrice = (decimal)prices.Average(),
                        ProductCount = products.Count
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCategoryPriceInfo Error: {ex.Message}");
                return null;
            }
        }
        
    /// <summary>
    /// ✅ LUỒNG 1: Load TẤT CẢ sản phẩm để gửi lên Gemini
    /// Format: "ID|Tên|Mô tả|Giá|Khuyến mãi|Category"
    /// </summary>
    public ProductDataForGemini GetAllProductsForGemini(int limit = 500)
        {
            try
            {
                // ✅ Khai báo products bên ngoài using block để có thể sử dụng sau khi using đóng
                var products = new List<dynamic>();
                
                using (var db = new ecommerceEntities())
                {
                    products = db.Products
                        .Where(p => p.Active == true)
                        .OrderByDescending(p => p.Stock > 0 ? 1 : 0) // Ưu tiên còn hàng
                        .ThenByDescending(p => p.SalePrice > 0 && p.SalePrice < p.Price ? 1 : 0) // Ưu tiên khuyến mãi
                        .Take(limit)
                        .Select(p => new
                        {
                            ProductID = p.ProductID,
                            ProductName = p.ProductName ?? "",
                            Detail = p.Detail ?? "",
                            Price = p.Price,
                            SalePrice = p.SalePrice,
                            CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : "",
                            Stock = p.Stock
                        })
                        .ToList<dynamic>();
                } // ✅ Đóng using block sau khi đã materialize products
                    
                var sb = new StringBuilder();
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("📦 DANH SÁCH SẢN PHẨM");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("⚠️ QUAN TRỌNG: Chỉ trả về ProductID (số) tương ứng với sản phẩm phù hợp!");
                sb.AppendLine("");
                sb.AppendLine("Format mỗi sản phẩm:");
                sb.AppendLine("  ProductID: [ID]");
                sb.AppendLine("  Tên: [Tên sản phẩm]");
                sb.AppendLine("  Mô tả: [Mô tả ngắn]");
                sb.AppendLine("  Giá gốc: [Giá]đ");
                sb.AppendLine("  Giá khuyến mãi: [Giá KM]đ (nếu có)");
                sb.AppendLine("  Danh mục: [Danh mục]");
                sb.AppendLine("  Tình trạng: [Còn hàng/Hết hàng]");
                sb.AppendLine("");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("");
                
                foreach (var p in products)
                {
                    var detail = (p.Detail ?? "").Length > 200 
                        ? (p.Detail ?? "").Substring(0, 200) + "..." 
                        : (p.Detail ?? "");
                    
                    var promotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice.ToString("N0") : "Không có";
                    var status = p.Stock > 0 ? "Còn hàng" : "Hết hàng";
                    var finalPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? p.SalePrice : p.Price;
                    
                    sb.AppendLine($"ProductID: {p.ProductID}");
                    sb.AppendLine($"  Tên: {p.ProductName}");
                    sb.AppendLine($"  Mô tả: {detail}");
                    sb.AppendLine($"  Giá gốc: {p.Price:N0}đ");
                    sb.AppendLine($"  Giá khuyến mãi: {promotionPrice}");
                    sb.AppendLine($"  Giá cuối cùng: {finalPrice:N0}đ");
                    sb.AppendLine($"  Danh mục: {p.CategoryName}");
                    sb.AppendLine($"  Tình trạng: {status}");
                    sb.AppendLine("");
                }
                
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    
                var result = sb.ToString();
                var productIds = products.Select(p => (int)p.ProductID).ToList();
                System.Diagnostics.Debug.WriteLine($"📦 [ProductIndexService] GetAllProductsForGemini: Loaded {products.Count} sản phẩm, {result.Length} chars, {productIds.Count} ProductID");
                return new ProductDataForGemini
                {
                    Data = result,
                    ProductIds = productIds
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [ProductIndexService] GetAllProductsForGemini Error: {ex.Message}");
                return new ProductDataForGemini
                {
                    Data = "",
                    ProductIds = new List<int>()
                };
            }
        }
        
        /// <summary>
        /// ✅ Query lại CSDL theo ProductID để lấy thông tin chi tiết
        /// </summary>
        public List<ProductSearchResult> GetProductsByIds(List<int> productIds)
        {
            try
            {
                if (productIds == null || productIds.Count == 0)
                    return new List<ProductSearchResult>();
                
                using (var db = new ecommerceEntities())
                {
                    // Load hình ảnh trước
                    var allImages = db.ImageProducts
                        .Where(img => productIds.Contains(img.ProductID) && 
                                     img.ImagePath != null && 
                                     !string.IsNullOrEmpty(img.ImagePath))
                        .GroupBy(img => img.ProductID)
                        .ToDictionary(g => g.Key, g => g.OrderBy(img => img.ImageID).Select(img => img.ImagePath).FirstOrDefault());
                    
                    // ✅ Query sản phẩm theo ProductID và log chi tiết để debug
                    var products = db.Products
                        .Where(p => productIds.Contains(p.ProductID) && p.Active == true)
                        .ToList()
                        .OrderBy(p => productIds.IndexOf(p.ProductID)) // Giữ nguyên thứ tự
                        .Select(p => {
                            var imagePath = allImages.ContainsKey(p.ProductID) ? allImages[p.ProductID] : null;
                            
                            // ✅ Log chi tiết để debug
                            System.Diagnostics.Debug.WriteLine($"🔍 [ProductIndexService] GetProductsByIds - ProductID: {p.ProductID}, Name: {p.ProductName}, Price: {p.Price:N0}đ, ImagePath: {(string.IsNullOrEmpty(imagePath) ? "NULL" : imagePath)}");
                            
                            return new ProductSearchResult
                            {
                                ProductID = p.ProductID,
                                Name = p.ProductName,
                                Description = p.Detail,
                                Price = p.Price,
                                PromotionPrice = p.SalePrice > 0 && p.SalePrice < p.Price ? (decimal?)p.SalePrice : null,
                                CategoryName = p.GroupProduct != null ? p.GroupProduct.GroupName : null,
                                TotalQuantity = p.Stock,
                                ImagePath = imagePath,
                                RelevanceScore = 1.0 // Highest score vì được Gemini chọn
                            };
                        })
                        .ToList();
                    
                    // ✅ Validate: Kiểm tra xem có ProductID nào không tìm thấy không
                    var foundProductIds = products.Select(p => p.ProductID).ToList();
                    var missingProductIds = productIds.Where(id => !foundProductIds.Contains(id)).ToList();
                    
                    if (missingProductIds.Any())
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ [ProductIndexService] GetProductsByIds: Không tìm thấy {missingProductIds.Count} ProductID trong CSDL: [{string.Join(", ", missingProductIds)}]");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✅ [ProductIndexService] GetProductsByIds: Loaded {products.Count} sản phẩm từ {productIds.Count} ProductID yêu cầu");
                    
                    // ✅ Log tất cả sản phẩm trả về
                    foreach (var p in products)
                    {
                        System.Diagnostics.Debug.WriteLine($"   ✅ ProductID: {p.ProductID}, Name: {p.Name}, Price: {p.Price:N0}đ");
                    }
                    
                    return products;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [ProductIndexService] GetProductsByIds Error: {ex.Message}");
                return new List<ProductSearchResult>();
            }
        }
    }

    #region Models
    
    /// <summary>
    /// Kết quả trả về từ GetAllProductsForGemini, bao gồm data string và danh sách ProductID
    /// </summary>
    public class ProductDataForGemini
    {
        public string Data { get; set; }
        public List<int> ProductIds { get; set; }
    }

    public class ProductSearchResult
    {
        public int ProductID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal? PromotionPrice { get; set; }
        public string CategoryName { get; set; }
        public int TotalQuantity { get; set; }
        public string ImagePath { get; set; }
        public double RelevanceScore { get; set; }
    }

    public class CategoryInfo
    {
        public string CategoryName { get; set; }
        public int ProductCount { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal AvgPrice { get; set; }
    }

    public class CategoryPriceInfo
    {
        public string CategoryName { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal AvgPrice { get; set; }
        public int ProductCount { get; set; }
    }

    #endregion
}


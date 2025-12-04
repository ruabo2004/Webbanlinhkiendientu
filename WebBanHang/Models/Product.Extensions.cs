using System.Linq;

namespace WebBanLinhKienDienTu.Models
{
    /// <summary>
    /// Partial class extension for Product model
    /// This file is safe to edit (not auto-generated)
    /// </summary>
    public partial class Product
    {
        /// <summary>
        /// Check if product is on sale
        /// </summary>
        /// <returns>True if SalePrice is greater than 0 and less than Price</returns>
        public bool isSale()
        {
            return SalePrice > 0 && SalePrice < Price;
        }

        /// <summary>
        /// Calculate average rating from comments
        /// </summary>
        /// <returns>Average rating (0-5), or 0 if no ratings</returns>
        public double AverageRating()
        {
            if (Comments == null || !Comments.Any())
                return 0;

            var ratedComments = Comments.Where(c => c.Rate.HasValue && c.Rate.Value > 0);
            if (!ratedComments.Any())
                return 0;

            return ratedComments.Average(c => c.Rate.Value);
        }

        /// <summary>
        /// Get total number of ratings
        /// </summary>
        /// <returns>Total count of rated comments</returns>
        public int TotalRatings()
        {
            if (Comments == null)
                return 0;

            return Comments.Count(c => c.Rate.HasValue && c.Rate.Value > 0);
        }
    }

    /// <summary>
    /// Additional properties for Product (not in auto-generated model)
    /// These properties should be added to the database manually
    /// </summary>
    public partial class Product
    {
        /// <summary>
        /// Indicates if this is a new product (true) or old product (false)
        /// Default: false (old product)
        /// </summary>
        public bool IsNew { get; set; }
    }
}

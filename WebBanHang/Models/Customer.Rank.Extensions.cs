using System.Linq;
using WebBanLinhKienDienTu.Core;

namespace WebBanLinhKienDienTu.Models
{
    public partial class Customer
    {
        /// <summary>
        /// Tính tổng tiền đã chi (chỉ tính đơn hoàn thành & đã thanh toán)
        /// </summary>
        public long CalculateTotalSpent()
        {
            if (Orders == null || !Orders.Any())
                return 0;

            return Orders
                .Where(o => o.OrderStatusID == 3 && o.Paid) // Status 3 = Complete
                .Sum(o => (long?)o.TotalPrice) ?? 0;
        }

        /// <summary>
        /// Cập nhật TotalSpent và Rank
        /// </summary>
        public void UpdateRankAndSpent(UnitOfWork repository)
        {
            // Tính tổng tiền
            TotalSpent = CalculateTotalSpent();

            // Tìm rank phù hợp
            var ranks = repository.MemberRank.FetchAll()
                .Where(r => r.Active)
                .OrderByDescending(r => r.MinSpending)
                .ToList();

            foreach (var rank in ranks)
            {
                if (rank.IsInRange(TotalSpent))
                {
                    RankID = rank.RankID;
                    break;
                }
            }
        }

        /// <summary>
        /// Lấy % giảm giá theo rank
        /// </summary>
        public decimal GetRankDiscount()
        {
            return MemberRank?.DiscountPercent ?? 0;
        }

        /// <summary>
        /// Kiểm tra xem có được giảm giá theo rank không
        /// </summary>
        public bool HasRankDiscount()
        {
            return MemberRank != null && MemberRank.DiscountPercent > 0;
        }
    }
}


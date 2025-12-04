using System.Data.Entity;
using System.Linq;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Core.RepositoryModel
{
    public class MemberRankRepository : RepositoryModel<MemberRank>
    {
        public MemberRankRepository(DbContext db)
            : base(db)
        {
        }

        /// <summary>
        /// Lấy rank phù hợp dựa trên tổng tiền đã chi
        /// </summary>
        public MemberRank GetRankByTotalSpent(long totalSpent)
        {
            return FetchAll()
                .Where(r => r.Active)
                .Where(r => totalSpent >= r.MinSpending)
                .Where(r => r.MaxSpending == null || totalSpent <= r.MaxSpending)
                .OrderByDescending(r => r.MinSpending)
                .FirstOrDefault();
        }

        /// <summary>
        /// Lấy tất cả ranks đang active
        /// </summary>
        public IQueryable<MemberRank> GetActiveRanks()
        {
            return FetchAll().Where(r => r.Active).OrderBy(r => r.MinSpending);
        }
    }
}


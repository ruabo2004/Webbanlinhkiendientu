using System;

namespace WebBanLinhKienDienTu.Models
{
    public partial class MemberRank
    {
        /// <summary>
        /// Kiểm tra xem tổng tiền có nằm trong rank này không
        /// </summary>
        public bool IsInRange(long totalSpent)
        {
            if (!Active) return false;
            
            bool aboveMin = totalSpent >= MinSpending;
            bool belowMax = MaxSpending == null || totalSpent <= MaxSpending.Value;
            
            return aboveMin && belowMax;
        }

        /// <summary>
        /// Lấy rank tiếp theo
        /// </summary>
        public string GetNextRankName()
        {
            switch (RankID)
            {
                case 1: return "Đồng";
                case 2: return "Bạc";
                case 3: return "Vàng";
                case 4: return "Bạch Kim";
                case 5: return "Bạch Kim (Max)";
                default: return "";
            }
        }

        /// <summary>
        /// Số tiền cần để lên rank tiếp theo
        /// </summary>
        public long? GetAmountToNextRank(long currentSpent)
        {
            if (MaxSpending == null) return null; // Đã max rank
            return MaxSpending.Value - currentSpent + 1;
        }

        /// <summary>
        /// Phần trăm tiến độ đến rank tiếp theo
        /// </summary>
        public double GetProgressToNextRank(long currentSpent)
        {
            if (MaxSpending == null) return 100; // Đã max rank
            
            long range = MaxSpending.Value - MinSpending;
            if (range == 0) return 100;
            
            long progress = currentSpent - MinSpending;
            return Math.Min(100, (progress * 100.0) / range);
        }

        /// <summary>
        /// Lấy icon class dựa trên rank
        /// </summary>
        public string GetIconClass()
        {
            switch (RankID)
            {
                case 1: return "fa fa-user";           // Member
                case 2: return "fa fa-certificate";    // Đồng
                case 3: return "fa fa-star";           // Bạc
                case 4: return "fa fa-star-o";         // Vàng
                case 5: return "fa fa-diamond";        // Bạch Kim
                default: return "fa fa-user";
            }
        }

        /// <summary>
        /// Lấy gradient màu dựa trên rank (sử dụng ColorCode nếu có, nếu không thì dùng gradient mặc định)
        /// </summary>
        public string GetGradientColor()
        {
            // Nếu có ColorCode, tạo gradient từ ColorCode
            if (!string.IsNullOrEmpty(ColorCode))
            {
                // Tạo màu đậm hơn từ ColorCode
                var baseColor = ColorCode.TrimStart('#');
                if (baseColor.Length == 6)
                {
                    // Chuyển hex sang RGB
                    var r = Convert.ToInt32(baseColor.Substring(0, 2), 16);
                    var g = Convert.ToInt32(baseColor.Substring(2, 2), 16);
                    var b = Convert.ToInt32(baseColor.Substring(4, 2), 16);
                    
                    // Tạo màu đậm hơn (giảm độ sáng 20%)
                    var darkR = Math.Max(0, (int)(r * 0.8));
                    var darkG = Math.Max(0, (int)(g * 0.8));
                    var darkB = Math.Max(0, (int)(b * 0.8));
                    
                    var darkColor = $"#{darkR:X2}{darkG:X2}{darkB:X2}";
                    return $"linear-gradient(135deg, {ColorCode} 0%, {darkColor} 100%)";
                }
            }
            
            // Fallback: dùng gradient mặc định theo RankID
            switch (RankID)
            {
                case 1:
                    return "linear-gradient(135deg, #667eea 0%, #764ba2 100%)";
                case 2:
                    return "linear-gradient(135deg, #CD7F32 0%, #8B4513 100%)";
                case 3:
                    return "linear-gradient(135deg, #C0C0C0 0%, #808080 100%)";
                case 4:
                    return "linear-gradient(135deg, #FFD700 0%, #FFA500 100%)";
                case 5:
                    return "linear-gradient(135deg, #00D4FF 0%, #0099CC 100%)";
                default:
                    return "linear-gradient(135deg, #667eea 0%, #764ba2 100%)";
            }
        }
    }
}


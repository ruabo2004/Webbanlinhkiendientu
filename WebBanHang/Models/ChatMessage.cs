using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanLinhKienDienTu.Models
{
    public partial class ChatMessage
    {
        [Key]
        public int MessageID { get; set; }
        
        [Required]
        public int CustomerID { get; set; }
        
        [StringLength(4000)]
        public string Message { get; set; }
        
        public bool IsFromAdmin { get; set; }
        
        public bool IsRead { get; set; }
        
        public DateTime CreatedDate { get; set; }
        
        [ForeignKey("CustomerID")]
        public virtual Customer Customer { get; set; }
    }
}


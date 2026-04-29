using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class Account
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } // مثال: 1.1.1 (الأصول > المتداولة > الخزينة)

        [Required, MaxLength(100)]
        public string Name { get; set; } // مثال: الخزينة الرئيسية

        [MaxLength(50)]
        public string AccountType { get; set; } // أصل، خصم، ملكية، إيراد، مصروف

        [MaxLength(50)]
        public string SubType { get; set; } // متداول، ثابت، تشغيلي...

        public bool IsActive { get; set; } = true;
        public bool IsParent { get; set; } = false; // هل ده حساب رئيسي (ممنوع استخدامه في القيود)؟
        public int? ParentId { get; set; } // للشجرة المتداخلة
        public virtual Account Parent { get; set; }
        public virtual ICollection<Account> Children { get; set; }
        public decimal OpeningBalance { get; set; } = 0; // رصيد افتتاحي
        public string BalanceType { get; set; } = "Debit"; // طبيعة الحساب (مدين / دائن)

        // ده خاصية Helpers عشان تجيب الرصيد الحالي بسهولة
        public decimal CurrentBalance { get; set; }
    }
}
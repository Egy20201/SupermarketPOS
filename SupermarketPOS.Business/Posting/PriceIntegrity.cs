using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Pre-commit price/discount integrity rules. Reject any document whose lines
    /// have negative prices/quantities or whose discount falls outside configured limits.
    /// </summary>
    public static class PriceIntegrity
    {
        public readonly struct Line
        {
            public Line(decimal quantity, decimal unitPrice, decimal discount)
            {
                Quantity = quantity;
                UnitPrice = unitPrice;
                Discount = discount;
            }

            public decimal Quantity { get; }
            public decimal UnitPrice { get; }
            /// <summary>Absolute monetary discount applied to the line (not a percent).</summary>
            public decimal Discount { get; }
        }

        public sealed class Limits
        {
            public Limits(decimal maxLineDiscountPercent, decimal maxDocumentDiscountPercent)
            {
                if (maxLineDiscountPercent < 0m || maxLineDiscountPercent > 100m)
                    throw new ArgumentOutOfRangeException(nameof(maxLineDiscountPercent));
                if (maxDocumentDiscountPercent < 0m || maxDocumentDiscountPercent > 100m)
                    throw new ArgumentOutOfRangeException(nameof(maxDocumentDiscountPercent));
                MaxLineDiscountPercent = maxLineDiscountPercent;
                MaxDocumentDiscountPercent = maxDocumentDiscountPercent;
            }

            public decimal MaxLineDiscountPercent { get; }
            public decimal MaxDocumentDiscountPercent { get; }

            public static Limits Default { get; } = new Limits(100m, 100m);
        }

        public static string Validate(IEnumerable<Line> lines, decimal documentDiscountPercent, Limits limits)
        {
            if (lines == null) return "بيانات الأصناف غير صالحة";
            limits = limits ?? Limits.Default;
            if (documentDiscountPercent < 0m) return "قيمة الخصم غير صالحة";
            if (documentDiscountPercent > limits.MaxDocumentDiscountPercent)
                return "نسبة الخصم تتجاوز الحد المسموح";

            var any = false;
            foreach (var line in lines)
            {
                any = true;
                if (line.Quantity <= 0m) return "الكمية غير صالحة";
                if (line.UnitPrice < 0m) return "السعر لا يمكن أن يكون سالبًا";
                if (line.Discount < 0m) return "الخصم لا يمكن أن يكون سالبًا";
                var gross = line.Quantity * line.UnitPrice;
                if (line.Discount > gross) return "الخصم يتجاوز قيمة الصنف";
                if (gross > 0m)
                {
                    var pct = line.Discount / gross * 100m;
                    if (pct > limits.MaxLineDiscountPercent)
                        return "نسبة خصم الصنف تتجاوز الحد المسموح";
                }
            }
            if (!any) return "لا توجد أصناف";
            return null;
        }

        public static void EnsureValid(IEnumerable<Line> lines, decimal documentDiscountPercent, Limits limits)
        {
            var error = Validate(lines, documentDiscountPercent, limits);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }
    }
}

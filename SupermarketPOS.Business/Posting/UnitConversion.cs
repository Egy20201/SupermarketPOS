using System;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Centralised unit conversion. All quantity calculations across the system
    /// MUST normalise to the product's base unit through this helper so that
    /// inventory balances stay consistent even when sub-units (box -&gt; pieces) are used.
    /// </summary>
    public static class UnitConversion
    {
        public static int ToBaseQuantity(decimal quantity, decimal conversionFactor)
        {
            if (quantity < 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
            if (conversionFactor <= 0m) throw new ArgumentOutOfRangeException(nameof(conversionFactor), "Conversion factor must be positive.");
            var baseQty = quantity * conversionFactor;
            return (int)Math.Ceiling(baseQty);
        }

        public static decimal ToBaseQuantityDecimal(decimal quantity, decimal conversionFactor)
        {
            if (quantity < 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
            if (conversionFactor <= 0m) throw new ArgumentOutOfRangeException(nameof(conversionFactor), "Conversion factor must be positive.");
            return quantity * conversionFactor;
        }

        public static decimal FromBaseQuantity(decimal baseQuantity, decimal conversionFactor)
        {
            if (baseQuantity < 0m) throw new ArgumentOutOfRangeException(nameof(baseQuantity));
            if (conversionFactor <= 0m) throw new ArgumentOutOfRangeException(nameof(conversionFactor));
            return baseQuantity / conversionFactor;
        }
    }
}

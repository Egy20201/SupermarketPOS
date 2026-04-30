using System;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Pure over-return guard: the cumulative returned quantity for a given
    /// source line must never exceed the originally sold/purchased quantity.
    /// </summary>
    public static class ReturnQuantityGuard
    {
        public static bool IsAllowed(int originalQuantity, int previouslyReturned, int requested)
        {
            if (originalQuantity <= 0) return false;
            if (previouslyReturned < 0 || requested <= 0) return false;
            return previouslyReturned + requested <= originalQuantity;
        }

        public static void EnsureAllowed(int originalQuantity, int previouslyReturned, int requested, string productName)
        {
            if (!IsAllowed(originalQuantity, previouslyReturned, requested))
                throw new InvalidOperationException(
                    "كمية المرتجع أكبر من الكمية الأصلية" +
                    (string.IsNullOrWhiteSpace(productName) ? string.Empty : ": " + productName));
        }
    }
}

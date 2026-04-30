using System;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Once a document is in a terminal/posted state it MUST be immutable. Any
    /// attempt to amend it should throw — corrections must go through a return
    /// or reversal flow that emits its own balanced journal entry.
    /// </summary>
    public static class PostedDocumentGuard
    {
        public static bool IsPosted(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var normalised = status.Trim();
            return normalised.Equals("Posted", StringComparison.OrdinalIgnoreCase)
                || normalised.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                || normalised.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                || normalised.Equals("Approved", StringComparison.OrdinalIgnoreCase);
        }

        public static void EnsureMutable(string status)
        {
            if (IsPosted(status))
                throw new InvalidOperationException("لا يمكن تعديل مستند مرحَّل");
        }

        public static void EnsureMutable(string status, string documentNumber)
        {
            if (IsPosted(status))
                throw new InvalidOperationException(
                    "لا يمكن تعديل مستند مرحَّل" +
                    (string.IsNullOrWhiteSpace(documentNumber) ? string.Empty : ": " + documentNumber));
        }
    }
}

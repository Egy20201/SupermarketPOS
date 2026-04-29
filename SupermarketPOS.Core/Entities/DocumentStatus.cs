namespace SupermarketPOS.Core.Entities
{
    /// <summary>
    /// Standard document lifecycle statuses.
    /// Applied to all workflow documents (Quotations, Orders, Invoices, etc.)
    /// </summary>
    public enum DocumentStatus
    {
        Draft = 1,
        Sent = 2,
        Approved = 3,
        Confirmed = 4,
        Posted = 5,
        PartiallyDelivered = 6,
        Completed = 7,
        Shipped = 8,
        Delivered = 9,
        Accepted = 10,
        Rejected = 11,
        Cancelled = 12,
        Pending = 13
    }
}
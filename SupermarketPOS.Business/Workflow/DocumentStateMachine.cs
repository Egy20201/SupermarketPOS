using SupermarketPOS.Core.Entities;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Canonical document state machine, built once.
    /// Captures the existing document lifecycle (Draft → Sent/Approved/...)
    /// plus the explicit Pending review path:
    /// Draft → Pending → Approved → Rejected.
    /// </summary>
    public static class DocumentStateMachine
    {
        public static StateMachine<DocumentStatus> Instance { get; } = Build();

        private static StateMachine<DocumentStatus> Build()
        {
            var b = new StateMachineBuilder<DocumentStatus>();
            b.Allow(DocumentStatus.Draft, DocumentStatus.Pending, DocumentStatus.Sent, DocumentStatus.Approved, DocumentStatus.Cancelled);
            b.Allow(DocumentStatus.Pending, DocumentStatus.Approved, DocumentStatus.Rejected, DocumentStatus.Cancelled);
            b.Allow(DocumentStatus.Sent, DocumentStatus.Accepted, DocumentStatus.Rejected);
            b.Allow(DocumentStatus.Accepted, DocumentStatus.Confirmed, DocumentStatus.Rejected);
            b.Allow(DocumentStatus.Approved, DocumentStatus.Confirmed, DocumentStatus.Posted, DocumentStatus.Cancelled);
            b.Allow(DocumentStatus.Confirmed, DocumentStatus.Shipped, DocumentStatus.Posted, DocumentStatus.Cancelled);
            b.Allow(DocumentStatus.Shipped, DocumentStatus.Delivered, DocumentStatus.PartiallyDelivered);
            b.Allow(DocumentStatus.PartiallyDelivered, DocumentStatus.Delivered, DocumentStatus.Completed);
            b.Allow(DocumentStatus.Delivered, DocumentStatus.Completed);
            b.Allow(DocumentStatus.Posted, DocumentStatus.Cancelled);
            b.Final(DocumentStatus.Completed, DocumentStatus.Rejected, DocumentStatus.Cancelled);
            return b.Build();
        }
    }
}

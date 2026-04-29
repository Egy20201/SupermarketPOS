using SupermarketPOS.Core.Entities;
using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business
{
    /// <summary>
    /// Centralized workflow transition engine.
    /// All status changes must go through this class.
    /// </summary>
    public static class WorkflowEngine
    {
        private static readonly Dictionary<DocumentStatus, List<DocumentStatus>> _transitions
            = new Dictionary<DocumentStatus, List<DocumentStatus>>
        {
            { DocumentStatus.Draft, new List<DocumentStatus> { DocumentStatus.Sent, DocumentStatus.Approved, DocumentStatus.Cancelled } },
            { DocumentStatus.Sent, new List<DocumentStatus> { DocumentStatus.Accepted, DocumentStatus.Rejected } },
            { DocumentStatus.Accepted, new List<DocumentStatus> { DocumentStatus.Confirmed, DocumentStatus.Rejected } },
            { DocumentStatus.Approved, new List<DocumentStatus> { DocumentStatus.Confirmed, DocumentStatus.Posted, DocumentStatus.Cancelled } },
            { DocumentStatus.Confirmed, new List<DocumentStatus> { DocumentStatus.Shipped, DocumentStatus.Posted, DocumentStatus.Cancelled } },
            { DocumentStatus.Shipped, new List<DocumentStatus> { DocumentStatus.Delivered, DocumentStatus.PartiallyDelivered } },
            { DocumentStatus.PartiallyDelivered, new List<DocumentStatus> { DocumentStatus.Delivered, DocumentStatus.Completed } },
            { DocumentStatus.Delivered, new List<DocumentStatus> { DocumentStatus.Completed } },
            { DocumentStatus.Posted, new List<DocumentStatus> { DocumentStatus.Cancelled } },
            { DocumentStatus.Completed, new List<DocumentStatus>() },
            { DocumentStatus.Rejected, new List<DocumentStatus>() },
            { DocumentStatus.Cancelled, new List<DocumentStatus>() }
        };

        /// <summary>
        /// Returns valid next statuses for a given current status.
        /// </summary>
        public static List<DocumentStatus> GetValidTransitions(DocumentStatus current)
        {
            return _transitions.ContainsKey(current)
                ? _transitions[current]
                : new List<DocumentStatus>();
        }

        /// <summary>
        /// Attempts to transition a document to a new status.
        /// Returns true if valid; throws if invalid.
        /// </summary>
        public static void Transition(BaseDocument document, DocumentStatus newStatus)
        {
            var valid = GetValidTransitions(document.Status);

            if (!valid.Contains(newStatus))
            {
                throw new InvalidOperationException(
                    $"Cannot transition {document.DocumentType} from {document.Status} to {newStatus}. " +
                    $"Valid transitions: {string.Join(", ", valid)}");
            }

            document.Status = newStatus;
        }

        /// <summary>
        /// Checks if a transition is valid without throwing.
        /// </summary>
        public static bool CanTransition(DocumentStatus current, DocumentStatus target)
        {
            return GetValidTransitions(current).Contains(target);
        }

        /// <summary>
        /// Determines if a document is in a final state (cannot be modified).
        /// </summary>
        public static bool IsFinalState(DocumentStatus status)
        {
            return status == DocumentStatus.Completed ||
                   status == DocumentStatus.Cancelled ||
                   status == DocumentStatus.Rejected;
        }

        /// <summary>
        /// Determines if a document can be edited.
        /// </summary>
        public static bool CanEdit(DocumentStatus status)
        {
            return status == DocumentStatus.Draft;
        }
    }
}
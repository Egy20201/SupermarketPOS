using SupermarketPOS.Business.Workflow;
using SupermarketPOS.Core.Entities;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    /// <summary>
    /// Centralized workflow transition engine.
    /// All status changes must go through this class.
    /// Backed by the generic <see cref="StateMachine{TState}"/>; see
    /// <see cref="DocumentStateMachine"/> for the canonical transition table.
    /// </summary>
    /// <remarks>
    /// Methods remain static so existing call sites (<c>WorkflowEngine.Transition(...)</c>)
    /// keep compiling unchanged, but the surrounding type is non-static so it can
    /// also be injected as a constructor dependency by services that prefer DI.
    /// </remarks>
    public class WorkflowEngine
    {
        private static readonly StateMachine<DocumentStatus> _machine = DocumentStateMachine.Instance;

        /// <summary>
        /// Returns valid next statuses for a given current status.
        /// </summary>
        public static List<DocumentStatus> GetValidTransitions(DocumentStatus current)
        {
            return _machine.GetValidTransitions(current).ToList();
        }

        /// <summary>
        /// Attempts to transition a document to a new status.
        /// Returns true if valid; throws if invalid.
        /// </summary>
        public static void Transition(BaseDocument document, DocumentStatus newStatus)
        {
            document.Status = _machine.Transition(document.Status, newStatus);
        }

        /// <summary>
        /// Checks if a transition is valid without throwing.
        /// </summary>
        public static bool CanTransition(DocumentStatus current, DocumentStatus target)
        {
            return _machine.CanTransition(current, target);
        }

        /// <summary>
        /// Determines if a document is in a final state (cannot be modified).
        /// </summary>
        public static bool IsFinalState(DocumentStatus status)
        {
            return _machine.IsFinal(status);
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

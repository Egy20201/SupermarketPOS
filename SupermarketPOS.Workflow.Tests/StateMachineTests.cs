using System;
using SupermarketPOS.Business.Workflow;
using SupermarketPOS.Core.Entities;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class StateMachineTests
    {
        [Fact]
        public void DraftToPendingToApprovedIsAllowed()
        {
            var sm = DocumentStateMachine.Instance;
            Assert.True(sm.CanTransition(DocumentStatus.Draft, DocumentStatus.Pending));
            Assert.True(sm.CanTransition(DocumentStatus.Pending, DocumentStatus.Approved));
        }

        [Fact]
        public void PendingToRejectedIsAllowed()
        {
            Assert.True(DocumentStateMachine.Instance.CanTransition(DocumentStatus.Pending, DocumentStatus.Rejected));
        }

        [Fact]
        public void DraftDirectlyToRejectedIsRejected()
        {
            // Draft cannot be Rejected without first going through Pending or Sent.
            Assert.False(DocumentStateMachine.Instance.CanTransition(DocumentStatus.Draft, DocumentStatus.Rejected));
        }

        [Fact]
        public void TransitionThrowsOnInvalidTarget()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DocumentStateMachine.Instance.Transition(DocumentStatus.Completed, DocumentStatus.Draft));
            Assert.Contains("Cannot transition", ex.Message);
        }

        [Fact]
        public void FinalStatesAreTerminal()
        {
            var sm = DocumentStateMachine.Instance;
            Assert.True(sm.IsFinal(DocumentStatus.Completed));
            Assert.True(sm.IsFinal(DocumentStatus.Rejected));
            Assert.True(sm.IsFinal(DocumentStatus.Cancelled));
            Assert.Empty(sm.GetValidTransitions(DocumentStatus.Completed));
            Assert.Empty(sm.GetValidTransitions(DocumentStatus.Rejected));
            Assert.Empty(sm.GetValidTransitions(DocumentStatus.Cancelled));
        }

        [Fact]
        public void GenericBuilderEnforcesTransitions()
        {
            var sm = new StateMachineBuilder<string>(StringComparer.OrdinalIgnoreCase)
                .Allow("draft", "pending")
                .Allow("pending", "approved", "rejected")
                .Final("approved", "rejected")
                .Build();

            Assert.True(sm.CanTransition("draft", "pending"));
            Assert.True(sm.CanTransition("DRAFT", "PENDING"));
            Assert.False(sm.CanTransition("draft", "approved"));
            Assert.True(sm.IsFinal("approved"));
        }
    }
}

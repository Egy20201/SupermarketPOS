using SupermarketPOS.Business.Posting;
using System;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class PostedDocumentGuardTests
    {
        [Theory]
        [InlineData("Posted")]
        [InlineData("posted")]
        [InlineData("Completed")]
        [InlineData("Cancelled")]
        [InlineData("Approved")]
        public void Recognises_terminal_states(string status)
        {
            Assert.True(PostedDocumentGuard.IsPosted(status));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Saved")]
        [InlineData("Pending")]
        [InlineData("")]
        [InlineData(null)]
        public void Treats_open_states_as_mutable(string status)
        {
            Assert.False(PostedDocumentGuard.IsPosted(status));
            PostedDocumentGuard.EnsureMutable(status);
        }

        [Fact]
        public void Mutating_posted_throws_with_document_number()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                PostedDocumentGuard.EnsureMutable("Posted", "S-123"));
            Assert.Contains("S-123", ex.Message);
        }
    }
}

using System;
using SupermarketPOS.Business.Workflow;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class WorkflowDefinitionCacheTests
    {
        [Fact]
        public void LoaderIsInvokedOnlyOncePerEntityType()
        {
            var calls = 0;
            var cache = new WorkflowDefinitionCache(entityType =>
            {
                calls++;
                return new WorkflowDefinition(entityType, Array.Empty<IWorkflowRule>());
            });

            var a1 = cache.Get("SalesOrder");
            var a2 = cache.Get("SalesOrder");
            var b1 = cache.Get("PurchaseOrder");

            Assert.Same(a1, a2);
            Assert.NotSame(a1, b1);
            Assert.Equal(2, calls);
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void InvalidateForcesReload()
        {
            var calls = 0;
            var cache = new WorkflowDefinitionCache(entityType =>
            {
                calls++;
                return new WorkflowDefinition(entityType, Array.Empty<IWorkflowRule>());
            });

            cache.Get("SalesOrder");
            cache.Invalidate("SalesOrder");
            cache.Get("SalesOrder");

            Assert.Equal(2, calls);
        }

        [Fact]
        public void InvalidateAllClearsEverything()
        {
            var cache = new WorkflowDefinitionCache(et => new WorkflowDefinition(et, Array.Empty<IWorkflowRule>()));
            cache.Get("A");
            cache.Get("B");
            cache.InvalidateAll();
            Assert.Equal(0, cache.Count);
        }
    }
}

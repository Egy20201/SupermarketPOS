using SupermarketPOS.Business.Posting;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class PriceIntegrityTests
    {
        private static readonly PriceIntegrity.Limits StrictLimits =
            new PriceIntegrity.Limits(50m, 30m);

        [Fact]
        public void Valid_lines_pass()
        {
            var lines = new[]
            {
                new PriceIntegrity.Line(2m, 100m, 10m),
                new PriceIntegrity.Line(1m, 50m, 0m)
            };
            Assert.Null(PriceIntegrity.Validate(lines, 10m, StrictLimits));
        }

        [Fact]
        public void Empty_lines_rejected()
        {
            Assert.Equal("لا توجد أصناف",
                PriceIntegrity.Validate(new PriceIntegrity.Line[0], 0m, PriceIntegrity.Limits.Default));
        }

        [Fact]
        public void Negative_unit_price_rejected()
        {
            var lines = new[] { new PriceIntegrity.Line(1m, -5m, 0m) };
            Assert.Equal("السعر لا يمكن أن يكون سالبًا",
                PriceIntegrity.Validate(lines, 0m, PriceIntegrity.Limits.Default));
        }

        [Fact]
        public void Zero_quantity_rejected()
        {
            var lines = new[] { new PriceIntegrity.Line(0m, 5m, 0m) };
            Assert.Equal("الكمية غير صالحة",
                PriceIntegrity.Validate(lines, 0m, PriceIntegrity.Limits.Default));
        }

        [Fact]
        public void Document_discount_above_limit_rejected()
        {
            var lines = new[] { new PriceIntegrity.Line(1m, 100m, 0m) };
            Assert.Equal("نسبة الخصم تتجاوز الحد المسموح",
                PriceIntegrity.Validate(lines, 40m, StrictLimits));
        }

        [Fact]
        public void Line_discount_above_limit_rejected()
        {
            // 60 / (1 * 100) = 60% > 50% line cap
            var lines = new[] { new PriceIntegrity.Line(1m, 100m, 60m) };
            Assert.Equal("نسبة خصم الصنف تتجاوز الحد المسموح",
                PriceIntegrity.Validate(lines, 0m, StrictLimits));
        }

        [Fact]
        public void Discount_exceeding_line_total_rejected()
        {
            var lines = new[] { new PriceIntegrity.Line(1m, 100m, 200m) };
            Assert.Equal("الخصم يتجاوز قيمة الصنف",
                PriceIntegrity.Validate(lines, 0m, PriceIntegrity.Limits.Default));
        }
    }
}

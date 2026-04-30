using SupermarketPOS.Business.Posting;
using System;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class UnitConversionTests
    {
        [Fact]
        public void Box_to_pieces_normalises_to_base_unit()
        {
            // 3 boxes * 12 pieces/box = 36 pieces
            Assert.Equal(36, UnitConversion.ToBaseQuantity(3m, 12m));
        }

        [Fact]
        public void Base_unit_keeps_quantity_intact()
        {
            Assert.Equal(5, UnitConversion.ToBaseQuantity(5m, 1m));
        }

        [Fact]
        public void Fractional_quantities_are_ceiled_to_avoid_under_decrement()
        {
            // 0.5 box * 12 = 6 pieces (whole), 0.6 * 10 = 6
            Assert.Equal(6, UnitConversion.ToBaseQuantity(0.5m, 12m));
            // 0.45 * 10 = 4.5 -> 5 (ceiling biases towards conservative stock movement)
            Assert.Equal(5, UnitConversion.ToBaseQuantity(0.45m, 10m));
        }

        [Fact]
        public void Negative_quantity_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitConversion.ToBaseQuantity(-1m, 1m));
        }

        [Fact]
        public void Zero_or_negative_factor_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitConversion.ToBaseQuantity(1m, 0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitConversion.ToBaseQuantity(1m, -1m));
        }

        [Fact]
        public void Round_trip_decimal_then_back_preserves_value()
        {
            var baseQty = UnitConversion.ToBaseQuantityDecimal(2m, 6m);
            Assert.Equal(12m, baseQty);
            Assert.Equal(2m, UnitConversion.FromBaseQuantity(baseQty, 6m));
        }
    }
}

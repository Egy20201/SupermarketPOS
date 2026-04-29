using SupermarketPOS.Core.Entities;
using System.Collections.Generic;

namespace SupermarketPOS.Business
{
    public class SaleRuleContext
    {
        public IDictionary<string, bool> FeatureFlags { get; set; }
        public IList<ProductAttribute> ProductAttributes { get; set; }
        public string BusinessType { get; set; }
    }
}

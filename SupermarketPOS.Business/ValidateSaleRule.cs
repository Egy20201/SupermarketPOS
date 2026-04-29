using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class ValidateSaleRule : IRule
    {
        public string Validate(SaleRequest request, SaleRuleContext context)
        {
            if (request == null)
                return "بيانات البيع غير صالحة";

            var items = request.Items?.Where(i => i != null && i.ProductId > 0).ToList() ?? new List<SaleRequestItem>();
            if (!items.Any())
                return "لا توجد منتجات";

            if (request.WarehouseId <= 0 || request.UserId <= 0 || string.IsNullOrWhiteSpace(request.InvoiceNumber))
                return "بيانات البيع غير مكتملة";

            if (request.DiscountPercent < 0 || request.DiscountPercent > 100)
                return "قيمة الخصم غير صالحة";

            if (items.Any(i => i.Quantity <= 0 || i.UnitPrice < 0))
                return "بيانات الأصناف غير صالحة";

            var isPharmacy = string.Equals(context?.BusinessType, "pharmacy", System.StringComparison.OrdinalIgnoreCase);
            var expiryTrackingEnabled = context?.FeatureFlags != null &&
                                        context.FeatureFlags.ContainsKey("expiry_tracking") &&
                                        context.FeatureFlags["expiry_tracking"];
            if (isPharmacy || expiryTrackingEnabled)
            {
                foreach (var item in items)
                {
                    var expiryAttribute = context?.ProductAttributes?
                        .FirstOrDefault(a => a.ProductId == item.ProductId && a.Key == "expiry_date");
                    if (expiryAttribute == null || string.IsNullOrWhiteSpace(expiryAttribute.Value))
                    {
                        if (isPharmacy)
                            return "بيانات الصلاحية مطلوبة";
                        continue;
                    }

                    System.DateTime expiryDate;
                    if (!System.DateTime.TryParse(expiryAttribute.Value, out expiryDate))
                    {
                        if (isPharmacy)
                            return "بيانات الصلاحية غير صالحة";
                        continue;
                    }

                    if (expiryDate.Date < System.DateTime.Today)
                        return "الصنف منتهي الصلاحية";
                }
            }

            return null;
        }
    }
}

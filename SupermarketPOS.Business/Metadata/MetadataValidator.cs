using System;
using System.Linq;
using System.Text;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Validates that the metadata registry loaded correctly from DB.
    /// Called at startup after LoadAll(). Logs detailed proof to the application log.
    /// </summary>
    public static class MetadataValidator
    {
        /// <summary>
        /// Runs all validation checks. Returns true if ALL checks pass.
        /// Writes detailed results to Logger.
        /// </summary>
        public static bool Validate(MetadataRegistryService registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var sb = new StringBuilder();
            sb.AppendLine("=== METADATA VALIDATION START ===");

            bool allPassed = true;

            // 1. Verify registry is loaded
            allPassed &= Check(sb, "Registry.IsLoaded", registry.IsLoaded);

            // 2. Verify Product entity exists with correct fields
            var product = registry.GetEntity("Product");
            allPassed &= Check(sb, "GetEntity('Product') != null", product != null);

            if (product != null)
            {
                allPassed &= Check(sb, "Product.TableName == 'Products'", product.TableName == "Products");
                allPassed &= Check(sb, "Product.Module.Name == 'Inventory'", product.Module?.Name == "Inventory");
                allPassed &= Check(sb, "Product.Fields.Count >= 3", product.Fields.Count >= 3);

                var nameField = product.Fields.FirstOrDefault(f => f.Name == "Name");
                allPassed &= Check(sb, "Product.Name field exists", nameField != null);
                allPassed &= Check(sb, "Product.Name.DataType == 'string'", nameField?.DataType == "string");
                allPassed &= Check(sb, "Product.Name.IsRequired == true", nameField?.IsRequired == true);

                var priceField = product.Fields.FirstOrDefault(f => f.Name == "Price");
                allPassed &= Check(sb, "Product.Price field exists", priceField != null);
                allPassed &= Check(sb, "Product.Price.DataType == 'decimal'", priceField?.DataType == "decimal");

                var qtyField = product.Fields.FirstOrDefault(f => f.Name == "Quantity");
                allPassed &= Check(sb, "Product.Quantity field exists", qtyField != null);
                allPassed &= Check(sb, "Product.Quantity.DataType == 'int'", qtyField?.DataType == "int");

                sb.AppendLine($"  Product fields: [{string.Join(", ", product.Fields.Select(f => $"{f.Name}:{f.DataType}"))}]");
            }

            // 3. Verify Customer entity
            var customer = registry.GetEntity("Customer");
            allPassed &= Check(sb, "GetEntity('Customer') != null", customer != null);
            if (customer != null)
            {
                allPassed &= Check(sb, "Customer.Fields.Count >= 2", customer.Fields.Count >= 2);
                sb.AppendLine($"  Customer fields: [{string.Join(", ", customer.Fields.Select(f => $"{f.Name}:{f.DataType}"))}]");
            }

            // 4. Verify SaleInvoice entity + relation
            var sale = registry.GetEntity("SaleInvoice");
            allPassed &= Check(sb, "GetEntity('SaleInvoice') != null", sale != null);
            if (sale != null)
            {
                var outRels = registry.GetOutgoingRelations("SaleInvoice");
                allPassed &= Check(sb, "SaleInvoice has outgoing relations", outRels.Count > 0);

                var custRel = outRels.FirstOrDefault(r => r.TargetEntity?.Name == "Customer");
                allPassed &= Check(sb, "SaleInvoice -> Customer relation exists", custRel != null);
                allPassed &= Check(sb, "Relation type == 'many2one'", custRel?.RelationType == "many2one");
                allPassed &= Check(sb, "Relation field == 'CustomerId'", custRel?.FieldName == "CustomerId");
            }

            // 5. Verify case-insensitive lookup
            var productLower = registry.GetEntity("product");
            allPassed &= Check(sb, "Case-insensitive: GetEntity('product') works", productLower != null);

            // 6. Verify GetAllEntities
            var all = registry.GetAllEntities();
            allPassed &= Check(sb, $"GetAllEntities().Count == {all.Count} (>= 3)", all.Count >= 3);

            // 7. Verify GetFields shortcut
            var fields = registry.GetFields("Product");
            allPassed &= Check(sb, "GetFields('Product') returns fields", fields.Count >= 3);

            // 8. Verify no hardcoded definitions (all have DB-assigned Ids > 0)
            if (product != null)
            {
                allPassed &= Check(sb, "Product.Id > 0 (from DB)", product.Id > 0);
                allPassed &= Check(sb, "Product.Fields all have Id > 0",
                    product.Fields.All(f => f.Id > 0));
            }

            // 9. Verify modules
            var modules = registry.GetAllModules();
            allPassed &= Check(sb, $"Modules loaded: {modules.Count} (>= 3)", modules.Count >= 3);

            sb.AppendLine(allPassed
                ? "=== METADATA VALIDATION: ALL CHECKS PASSED ==="
                : "=== METADATA VALIDATION: SOME CHECKS FAILED ===");

            if (allPassed)
                Logger.Info(sb.ToString());
            else
                Logger.Error(sb.ToString());

            return allPassed;
        }

        private static bool Check(StringBuilder sb, string name, bool condition)
        {
            sb.AppendLine(condition ? $"  [PASS] {name}" : $"  [FAIL] {name}");
            return condition;
        }
    }
}

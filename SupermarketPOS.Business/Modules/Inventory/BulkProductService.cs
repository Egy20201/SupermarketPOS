using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class BulkProductService
    {
        public BulkParseResult ParseExcel(string filePath, IReadOnlyDictionary<string, int> categoryLookup, IReadOnlyDictionary<string, int> unitLookup)
        {
            var result = new BulkParseResult();
            using (var workbook = new XLWorkbook(filePath))
            {
                var sheet = workbook.Worksheets.FirstOrDefault();
                if (sheet == null)
                {
                    return result;
                }

                var rows = sheet.RowsUsed().Skip(1).ToList();
                foreach (var row in rows)
                {
                    var rowResult = new BulkRowValidationResult { RowNumber = row.RowNumber() };
                    var request = new ProductSaveRequest
                    {
                        Name = row.Cell(1).GetString(),
                        Barcode = row.Cell(2).GetString(),
                        CategoryId = ResolveCategoryId(row.Cell(3).GetString(), categoryLookup),
                        Unit = row.Cell(4).GetString(),
                        UnitId = ResolveUnitId(row.Cell(4).GetString(), unitLookup),
                        PurchasePrice = ParseDecimal(row.Cell(5).GetString()),
                        SellingPrice = ParseDecimal(row.Cell(6).GetString()),
                        ReorderLevel = ParseInt(row.Cell(7).GetString())
                    };

                    rowResult.Request = request;
                    ValidateRow(rowResult);
                    result.Rows.Add(rowResult);
                }
            }

            return result;
        }

        public byte[] BuildTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Products");
                sheet.Cell(1, 1).Value = "Name";
                sheet.Cell(1, 2).Value = "Barcode";
                sheet.Cell(1, 3).Value = "Category";
                sheet.Cell(1, 4).Value = "Unit";
                sheet.Cell(1, 5).Value = "PurchasePrice";
                sheet.Cell(1, 6).Value = "SellingPrice";
                sheet.Cell(1, 7).Value = "ReorderLevel";
                sheet.Row(1).Style.Font.Bold = true;
                sheet.Columns().AdjustToContents();

                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private static int ResolveCategoryId(string categoryName, IReadOnlyDictionary<string, int> categoryLookup)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return 0;
            }

            var key = categoryName.Trim();
            return categoryLookup != null && categoryLookup.ContainsKey(key) ? categoryLookup[key] : 0;
        }

        private static int? ResolveUnitId(string unitName, IReadOnlyDictionary<string, int> unitLookup)
        {
            if (string.IsNullOrWhiteSpace(unitName))
            {
                return null;
            }

            var key = unitName.Trim();
            if (unitLookup != null && unitLookup.ContainsKey(key))
            {
                return unitLookup[key];
            }

            return null;
        }

        private static decimal ParseDecimal(string value)
        {
            decimal number;
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number) ||
                decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out number))
            {
                return number;
            }

            return -1m;
        }

        private static int ParseInt(string value)
        {
            int number;
            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number) ||
                int.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out number))
            {
                return number;
            }

            return -1;
        }

        private static void ValidateRow(BulkRowValidationResult row)
        {
            var request = row.Request;
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                row.Errors.Add("'D'3E E7DH(");
            }

            if (request.CategoryId <= 0)
            {
                row.Errors.Add("A&) :J1 5'D-)");
            }

            if (request.SellingPrice < 0m || request.PurchasePrice < 0m)
            {
                row.Errors.Add("'D#39'1 :J1 5'D-)");
            }

            if (request.ReorderLevel < 0)
            {
                row.Errors.Add("'D-/ 'D#/FI :J1 5'D-");
            }
        }
    }

    public class BulkParseResult
    {
        public List<BulkRowValidationResult> Rows { get; set; } = new List<BulkRowValidationResult>();
    }

    public class BulkRowValidationResult
    {
        public int RowNumber { get; set; }
        public ProductSaveRequest Request { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public bool IsValid => !Errors.Any();
    }
}


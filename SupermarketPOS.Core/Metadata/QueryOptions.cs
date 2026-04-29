using System.Collections.Generic;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Options for the generic query engine.
    /// Supports filtering, sorting, paging, and automatic lookup resolution.
    /// </summary>
    public class QueryOptions
    {
        /// <summary>Filters to apply (AND logic). Empty = no filtering.</summary>
        public List<FieldFilter> Filters { get; set; } = new List<FieldFilter>();

        /// <summary>Sort column. Null = ORDER BY Id ASC.</summary>
        public string SortField { get; set; }

        /// <summary>Sort descending. Default false (ASC).</summary>
        public bool SortDescending { get; set; }

        /// <summary>Page number (1-based). Default 1.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Rows per page. Default 50, max 1000.</summary>
        public int PageSize { get; set; } = 50;

        /// <summary>If true, lookup fields will be JOINed to return display values.</summary>
        public bool ResolveLookups { get; set; } = true;

        /// <summary>Columns to return. Empty = all metadata fields + Id.</summary>
        public List<string> SelectFields { get; set; } = new List<string>();

        /// <summary>Converts to the QueryRequest format used by the engine.</summary>
        public QueryRequest ToQueryRequest()
        {
            var request = new QueryRequest
            {
                Limit = PageSize,
                Offset = (Page - 1) * PageSize,
                SelectFields = SelectFields ?? new List<string>()
            };

            if (!string.IsNullOrWhiteSpace(SortField))
                request.SortBy.Add(new SortField { FieldName = SortField, Descending = SortDescending });

            if (Filters != null)
            {
                foreach (var f in Filters)
                    request.Filters.Add(new FilterCondition
                    {
                        FieldName = f.Field,
                        Operator = f.Operator ?? "eq",
                        Value = f.Value
                    });
            }

            return request;
        }
    }

    /// <summary>
    /// A single filter condition for QueryOptions.
    /// </summary>
    public class FieldFilter
    {
        /// <summary>Field name to filter on.</summary>
        public string Field { get; set; }

        /// <summary>Operator: eq, neq, gt, gte, lt, lte, contains, startswith, endswith, isnull, isnotnull</summary>
        public string Operator { get; set; } = "eq";

        /// <summary>Value to compare against.</summary>
        public object Value { get; set; }
    }
}

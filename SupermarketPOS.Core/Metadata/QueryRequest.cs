using System.Collections.Generic;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Describes a dynamic query against any metadata-registered entity.
    /// Used by the Generic Data Engine (Phase 2).
    /// </summary>
    public class QueryRequest
    {
        /// <summary>Filters to apply (AND logic). Empty = no filtering.</summary>
        public List<FilterCondition> Filters { get; set; } = new List<FilterCondition>();

        /// <summary>Columns to return. Empty = all metadata fields + Id.</summary>
        public List<string> SelectFields { get; set; } = new List<string>();

        /// <summary>Sort order. Empty = ORDER BY Id ASC.</summary>
        public List<SortField> SortBy { get; set; } = new List<SortField>();

        /// <summary>Max rows to return (SQL FETCH NEXT). Null = no limit.</summary>
        public int? Limit { get; set; }

        /// <summary>Rows to skip (SQL OFFSET). Null = 0.</summary>
        public int? Offset { get; set; }
    }

    /// <summary>
    /// A single WHERE condition.
    /// Operators: eq, neq, gt, gte, lt, lte, contains, startswith, endswith, isnull, isnotnull
    /// </summary>
    public class FilterCondition
    {
        public string FieldName { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
    }

    /// <summary>
    /// A single ORDER BY clause.
    /// </summary>
    public class SortField
    {
        public string FieldName { get; set; }
        public bool Descending { get; set; }
    }
}

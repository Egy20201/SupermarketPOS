using SupermarketPOS.Core.Metadata;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Builds parameterized SQL from metadata definitions.
    /// ALL user values go through SqlParameter — zero string concatenation.
    /// Table/column names are bracket-quoted against identifier injection.
    /// </summary>
    internal static class SqlQueryBuilder
    {
        private static readonly Dictionary<string, string> OperatorTemplates =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "eq",         "{0} = {1}" },
                { "neq",        "{0} <> {1}" },
                { "gt",         "{0} > {1}" },
                { "gte",        "{0} >= {1}" },
                { "lt",         "{0} < {1}" },
                { "lte",        "{0} <= {1}" },
                { "contains",   "{0} LIKE {1}" },
                { "startswith", "{0} LIKE {1}" },
                { "endswith",   "{0} LIKE {1}" },
                { "isnull",     "{0} IS NULL" },
                { "isnotnull",  "{0} IS NOT NULL" }
            };

        // ================================================================
        //  SELECT
        // ================================================================

        public static (string Sql, List<SqlParameter> Parameters) BuildSelect(
            EntityDefinition entity,
            IReadOnlyList<FieldDefinition> fields,
            QueryRequest request)
        {
            var parameters = new List<SqlParameter>();
            var sb = new StringBuilder();
            int pi = 0;

            // --- columns ---
            var columns = ResolveSelectColumns(fields, request.SelectFields);
            sb.Append("SELECT ");
            sb.Append(string.Join(", ", columns.Select(QuoteName)));
            sb.Append(" FROM ");
            sb.Append(QuoteName(entity.TableName));

            // --- WHERE ---
            if (request.Filters != null && request.Filters.Count > 0)
            {
                var clauses = BuildWhereClauses(fields, request.Filters, parameters, ref pi);
                if (clauses.Count > 0)
                {
                    sb.Append(" WHERE ");
                    sb.Append(string.Join(" AND ", clauses));
                }
            }

            // --- ORDER BY ---
            sb.Append(BuildOrderByClause(fields, request.SortBy));

            // --- OFFSET / FETCH ---
            if (request.Offset.HasValue || request.Limit.HasValue)
            {
                sb.Append(" OFFSET @pOffset ROWS");
                parameters.Add(new SqlParameter("@pOffset", request.Offset ?? 0));

                if (request.Limit.HasValue)
                {
                    sb.Append(" FETCH NEXT @pLimit ROWS ONLY");
                    parameters.Add(new SqlParameter("@pLimit", request.Limit.Value));
                }
            }

            return (sb.ToString(), parameters);
        }

        // ================================================================
        //  SELECT WITH LOOKUP RESOLUTION
        // ================================================================

        /// <summary>
        /// Builds a SELECT with LEFT JOINs for lookup fields.
        /// Lookup fields (DataType == "lookup") are joined to their target entity table
        /// and the display field (typically "Name") is returned as "{FieldName}_Display".
        /// </summary>
        public static (string Sql, List<SqlParameter> Parameters) BuildSelectWithLookups(
            EntityDefinition entity,
            IReadOnlyList<FieldDefinition> fields,
            QueryRequest request,
            Func<int, EntityDefinition> lookupResolver)
        {
            var parameters = new List<SqlParameter>();
            var sb = new StringBuilder();
            int pi = 0;
            string mainAlias = "t0";

            // --- columns ---
            var columns = ResolveSelectColumns(fields, request.SelectFields);
            var selectParts = new List<string>();
            var joinParts = new List<string>();
            int joinIndex = 0;

            // Always include Id
            selectParts.Add($"{mainAlias}.[Id]");

            foreach (var col in columns)
            {
                if (col.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var field = FindField(fields, col);
                selectParts.Add($"{mainAlias}.{QuoteName(col)}");

                // If lookup field, add JOIN + display column
                if (field != null
                    && field.DataType != null
                    && field.DataType.Equals("lookup", StringComparison.OrdinalIgnoreCase)
                    && field.LookupEntityId.HasValue
                    && lookupResolver != null)
                {
                    var targetEntity = lookupResolver(field.LookupEntityId.Value);
                    if (targetEntity != null)
                    {
                        joinIndex++;
                        var joinAlias = $"lk{joinIndex}";
                        var displayField = ResolveDisplayField(targetEntity);

                        joinParts.Add(
                            $"LEFT JOIN {QuoteName(targetEntity.TableName)} {joinAlias} " +
                            $"ON {mainAlias}.{QuoteName(field.Name)} = {joinAlias}.[Id]");

                        selectParts.Add(
                            $"{joinAlias}.{QuoteName(displayField)} AS {QuoteName(field.Name + "_Display")}");
                    }
                }
            }

            sb.Append("SELECT ");
            sb.Append(string.Join(", ", selectParts));
            sb.Append(" FROM ");
            sb.Append(QuoteName(entity.TableName));
            sb.Append(" ").Append(mainAlias);

            foreach (var join in joinParts)
            {
                sb.Append(" ");
                sb.Append(join);
            }

            // --- WHERE ---
            if (request.Filters != null && request.Filters.Count > 0)
            {
                var clauses = BuildWhereClausesAliased(fields, request.Filters, parameters, ref pi, mainAlias);
                if (clauses.Count > 0)
                {
                    sb.Append(" WHERE ");
                    sb.Append(string.Join(" AND ", clauses));
                }
            }

            // --- ORDER BY (aliased) ---
            sb.Append(BuildOrderByClauseAliased(fields, request.SortBy, mainAlias));

            // --- OFFSET / FETCH ---
            if (request.Offset.HasValue || request.Limit.HasValue)
            {
                sb.Append(" OFFSET @pOffset ROWS");
                parameters.Add(new SqlParameter("@pOffset", request.Offset ?? 0));

                if (request.Limit.HasValue)
                {
                    sb.Append(" FETCH NEXT @pLimit ROWS ONLY");
                    parameters.Add(new SqlParameter("@pLimit", request.Limit.Value));
                }
            }

            return (sb.ToString(), parameters);
        }

        /// <summary>
        /// Determines the display field for a lookup entity.
        /// Looks for "Name" field first, then uses the first visible string field.
        /// Falls back to "Id" if nothing else is available.
        /// </summary>
        private static string ResolveDisplayField(EntityDefinition entity)
        {
            var fields = entity.Fields;
            if (fields == null || fields.Count == 0) return "Id";

            // Prefer "Name" field
            var nameField = fields.FirstOrDefault(f =>
                f.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
            if (nameField != null) return nameField.Name;

            // Fallback: first visible string field
            var firstString = fields.FirstOrDefault(f =>
                f.IsVisible && f.DataType != null &&
                f.DataType.Equals("string", StringComparison.OrdinalIgnoreCase));
            if (firstString != null) return firstString.Name;

            return "Id";
        }

        // ================================================================
        //  INSERT
        // ================================================================

        public static (string Sql, List<SqlParameter> Parameters) BuildInsert(
            EntityDefinition entity,
            IReadOnlyList<FieldDefinition> fields,
            Dictionary<string, object> data)
        {
            var parameters = new List<SqlParameter>();
            var columnList = new List<string>();
            var paramList = new List<string>();
            int pi = 0;

            foreach (var kvp in data)
            {
                // Skip Id — auto-generated by DB
                if (kvp.Key.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fd = FindField(fields, kvp.Key);
                if (fd == null) continue;

                var paramName = $"@p{pi++}";
                var value = ConvertValue(kvp.Value, fd.DataType);

                columnList.Add(QuoteName(fd.Name));
                paramList.Add(paramName);
                parameters.Add(new SqlParameter(paramName, value ?? (object)DBNull.Value));
            }

            if (columnList.Count == 0)
                throw new InvalidOperationException("No valid columns to insert.");

            var sb = new StringBuilder();
            sb.Append("INSERT INTO ").Append(QuoteName(entity.TableName));
            sb.Append(" (").Append(string.Join(", ", columnList)).Append(")");
            sb.Append(" OUTPUT INSERTED.[Id]");
            sb.Append(" VALUES (").Append(string.Join(", ", paramList)).Append(")");

            return (sb.ToString(), parameters);
        }

        // ================================================================
        //  UPDATE
        // ================================================================

        public static (string Sql, List<SqlParameter> Parameters) BuildUpdate(
            EntityDefinition entity,
            IReadOnlyList<FieldDefinition> editableFields,
            int id,
            Dictionary<string, object> data)
        {
            var parameters = new List<SqlParameter>();
            var setClauses = new List<string>();
            int pi = 0;

            foreach (var kvp in data)
            {
                if (kvp.Key.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fd = FindField(editableFields, kvp.Key);
                if (fd == null) continue;

                var paramName = $"@p{pi++}";
                var value = ConvertValue(kvp.Value, fd.DataType);

                setClauses.Add($"{QuoteName(fd.Name)} = {paramName}");
                parameters.Add(new SqlParameter(paramName, value ?? (object)DBNull.Value));
            }

            if (setClauses.Count == 0)
                throw new InvalidOperationException("No valid editable columns to update.");

            parameters.Add(new SqlParameter("@pId", id));

            var sql = $"UPDATE {QuoteName(entity.TableName)} " +
                      $"SET {string.Join(", ", setClauses)} " +
                      $"WHERE [Id] = @pId";

            return (sql, parameters);
        }

        // ================================================================
        //  DELETE
        // ================================================================

        public static (string Sql, List<SqlParameter> Parameters) BuildDelete(
            EntityDefinition entity, int id)
        {
            var sql = $"DELETE FROM {QuoteName(entity.TableName)} WHERE [Id] = @pId";
            return (sql, new List<SqlParameter> { new SqlParameter("@pId", id) });
        }

        // ================================================================
        //  Helpers
        // ================================================================

        /// <summary>
        /// Resolve which columns to SELECT.
        /// If caller specified fields, use those (validated). Otherwise all metadata fields.
        /// Always includes [Id].
        /// </summary>
        private static List<string> ResolveSelectColumns(
            IReadOnlyList<FieldDefinition> fields, List<string> requested)
        {
            List<string> columns;

            if (requested != null && requested.Count > 0)
            {
                columns = requested
                    .Where(name =>
                        name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                        fields.Any(fd => fd.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
            else
            {
                columns = fields.Select(f => f.Name).ToList();
            }

            // Always include Id as first column
            if (!columns.Any(c => c.Equals("Id", StringComparison.OrdinalIgnoreCase)))
                columns.Insert(0, "Id");

            return columns;
        }

        /// <summary>
        /// Build WHERE clauses from filters. Handles all operator types including LIKE and IS NULL.
        /// </summary>
        private static List<string> BuildWhereClauses(
            IReadOnlyList<FieldDefinition> fields,
            List<FilterCondition> filters,
            List<SqlParameter> parameters,
            ref int pi)
        {
            return BuildWhereClausesCore(fields, filters, parameters, ref pi, null);
        }

        /// <summary>
        /// Build WHERE clauses with table alias prefix (for JOINed queries).
        /// </summary>
        private static List<string> BuildWhereClausesAliased(
            IReadOnlyList<FieldDefinition> fields,
            List<FilterCondition> filters,
            List<SqlParameter> parameters,
            ref int pi,
            string alias)
        {
            return BuildWhereClausesCore(fields, filters, parameters, ref pi, alias);
        }

        private static List<string> BuildWhereClausesCore(
            IReadOnlyList<FieldDefinition> fields,
            List<FilterCondition> filters,
            List<SqlParameter> parameters,
            ref int pi,
            string alias)
        {
            var clauses = new List<string>();

            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.FieldName))
                    continue;

                // Resolve column + datatype (Id is always valid)
                string columnName;
                string dataType;

                if (filter.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    columnName = "Id";
                    dataType = "int";
                }
                else
                {
                    var fd = FindField(fields, filter.FieldName);
                    if (fd == null)
                        throw new ArgumentException($"Filter field '{filter.FieldName}' not found in metadata.");
                    columnName = fd.Name;
                    dataType = fd.DataType;
                }

                var qualifiedColumn = alias != null
                    ? $"{alias}.{QuoteName(columnName)}"
                    : QuoteName(columnName);

                var op = filter.Operator ?? "eq";
                if (!OperatorTemplates.TryGetValue(op, out var template))
                    throw new ArgumentException(
                        $"Unknown operator '{op}'. Valid: {string.Join(", ", OperatorTemplates.Keys)}");

                // IS NULL / IS NOT NULL — no parameter needed
                if (op.Equals("isnull", StringComparison.OrdinalIgnoreCase) ||
                    op.Equals("isnotnull", StringComparison.OrdinalIgnoreCase))
                {
                    clauses.Add(string.Format(template, qualifiedColumn));
                    continue;
                }

                // Value-based operators
                if (filter.Value == null)
                    throw new ArgumentException(
                        $"Filter on '{columnName}' with operator '{op}' requires a non-null value.");

                var paramName = $"@p{pi++}";
                object sqlValue;

                // LIKE operators: wrap value with wildcards
                if (op.Equals("contains", StringComparison.OrdinalIgnoreCase))
                    sqlValue = $"%{filter.Value}%";
                else if (op.Equals("startswith", StringComparison.OrdinalIgnoreCase))
                    sqlValue = $"{filter.Value}%";
                else if (op.Equals("endswith", StringComparison.OrdinalIgnoreCase))
                    sqlValue = $"%{filter.Value}";
                else
                    sqlValue = ConvertValue(filter.Value, dataType);

                parameters.Add(new SqlParameter(paramName, sqlValue ?? (object)DBNull.Value));
                clauses.Add(string.Format(template, qualifiedColumn, paramName));
            }

            return clauses;
        }

        /// <summary>Build ORDER BY clause. Defaults to ORDER BY [Id] if no sort specified.</summary>
        private static string BuildOrderByClause(
            IReadOnlyList<FieldDefinition> fields, List<SortField> sortFields)
        {
            return BuildOrderByClauseCore(fields, sortFields, null);
        }

        /// <summary>Build ORDER BY clause with table alias prefix.</summary>
        private static string BuildOrderByClauseAliased(
            IReadOnlyList<FieldDefinition> fields, List<SortField> sortFields, string alias)
        {
            return BuildOrderByClauseCore(fields, sortFields, alias);
        }

        private static string BuildOrderByClauseCore(
            IReadOnlyList<FieldDefinition> fields, List<SortField> sortFields, string alias)
        {
            string Qualify(string col) => alias != null ? $"{alias}.{QuoteName(col)}" : QuoteName(col);

            if (sortFields == null || sortFields.Count == 0)
                return $" ORDER BY {Qualify("Id")}";

            var clauses = new List<string>();

            foreach (var sort in sortFields)
            {
                string columnName;
                if (sort.FieldName.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    columnName = "Id";
                else
                {
                    var fd = FindField(fields, sort.FieldName);
                    if (fd == null) continue;
                    columnName = fd.Name;
                }
                clauses.Add($"{Qualify(columnName)}{(sort.Descending ? " DESC" : " ASC")}");
            }

            return clauses.Count > 0
                ? " ORDER BY " + string.Join(", ", clauses)
                : $" ORDER BY {Qualify("Id")}";
        }

        /// <summary>Case-insensitive field lookup without LINQ allocation.</summary>
        private static FieldDefinition FindField(IReadOnlyList<FieldDefinition> fields, string name)
        {
            for (int i = 0; i < fields.Count; i++)
                if (fields[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return fields[i];
            return null;
        }

        /// <summary>
        /// Bracket-quote a SQL Server identifier.
        /// "Products" -> "[Products]",  "My]Table" -> "[My]]Table]"
        /// </summary>
        private static string QuoteName(string identifier)
        {
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        /// <summary>
        /// Convert a raw value to the CLR type matching the metadata DataType.
        /// Throws ArgumentException on conversion failure — fail fast, clear message.
        /// </summary>
        internal static object ConvertValue(object value, string dataType)
        {
            if (value == null || value is DBNull)
                return null;

            switch (dataType?.ToLowerInvariant())
            {
                case "string":
                    return value.ToString();

                case "int":
                    if (value is int) return value;
                    if (value is long longVal) return checked((int)longVal);
                    if (int.TryParse(value.ToString(), out var intResult)) return intResult;
                    throw new ArgumentException($"Cannot convert '{value}' to int.");

                case "number":
                case "decimal":
                    if (value is decimal) return value;
                    if (value is double dbl) return (decimal)dbl;
                    if (value is float flt) return (decimal)flt;
                    if (value is int intForDec) return (decimal)intForDec;
                    if (value is long longForDec) return (decimal)longForDec;
                    if (decimal.TryParse(value.ToString(), out var decResult)) return decResult;
                    throw new ArgumentException($"Cannot convert '{value}' to number.");

                case "date":
                case "datetime":
                    if (value is DateTime) return value;
                    if (DateTime.TryParse(value.ToString(), out var dtResult)) return dtResult;
                    throw new ArgumentException($"Cannot convert '{value}' to DateTime.");

                case "bool":
                case "boolean":
                    if (value is bool) return value;
                    if (bool.TryParse(value.ToString(), out var boolResult)) return boolResult;
                    var s = value.ToString().Trim();
                    if (s == "1") return true;
                    if (s == "0") return false;
                    throw new ArgumentException($"Cannot convert '{value}' to bool.");

                case "lookup":
                    // Lookup fields store an integer FK
                    if (value is int) return value;
                    if (value is long lv) return checked((int)lv);
                    if (int.TryParse(value.ToString(), out var lookupId)) return lookupId;
                    throw new ArgumentException($"Cannot convert '{value}' to lookup Id.");

                default:
                    return value;
            }
        }
    }
}

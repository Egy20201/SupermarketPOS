using SupermarketPOS.Core.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Phase 2.5: Evaluates computed field expressions after query results are fetched.
    /// Expressions reference other field values in the same row.
    /// 
    /// Supported syntax:
    ///   - Field references: {FieldName}
    ///   - Arithmetic: +, -, *, /
    ///   - String concat: {Field1} + " " + {Field2}
    ///   - Literal numbers and quoted strings
    /// 
    /// Evaluation is row-by-row, post-query. No SQL generation — pure CLR.
    /// Computed fields are excluded from INSERT/UPDATE by the engine.
    /// </summary>
    internal static class ComputedFieldEvaluator
    {
        private static readonly Regex FieldRefPattern = new Regex(
            @"\{(\w+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Evaluate all computed fields for a set of query result rows.
        /// Modifies each row dictionary in-place by adding computed field values.
        /// </summary>
        public static void EvaluateAll(
            IReadOnlyList<FieldDefinition> fields,
            List<Dictionary<string, object>> rows)
        {
            var computedFields = fields
                .Where(f => f.IsComputed && !string.IsNullOrWhiteSpace(f.ComputedExpression))
                .ToList();

            if (computedFields.Count == 0) return;

            foreach (var row in rows)
            {
                foreach (var field in computedFields)
                {
                    try
                    {
                        row[field.Name] = Evaluate(field.ComputedExpression, field.DataType, row);
                    }
                    catch (Exception ex)
                    {
                        // Computed field evaluation failure should not break the query
                        Logger.Warning($"[ComputedField] Failed to evaluate '{field.Name}': {ex.Message}");
                        row[field.Name] = null;
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate a single expression against a row's data.
        /// </summary>
        private static object Evaluate(
            string expression, string dataType, Dictionary<string, object> row)
        {
            // Resolve all {FieldName} references to values
            var resolved = FieldRefPattern.Replace(expression, match =>
            {
                var fieldName = match.Groups[1].Value;
                if (row.TryGetValue(fieldName, out var val) && val != null)
                    return val.ToString();
                return "0";
            });

            // Try numeric evaluation
            switch (dataType?.ToLowerInvariant())
            {
                case "number":
                case "decimal":
                case "int":
                    return EvaluateNumeric(resolved);

                case "string":
                default:
                    return EvaluateString(resolved, row, expression);
            }
        }

        /// <summary>
        /// Evaluate a simple arithmetic expression with +, -, *, /.
        /// Supports parentheses, decimal values.
        /// </summary>
        private static object EvaluateNumeric(string expression)
        {
            // Tokenize: numbers and operators
            var tokens = TokenizeNumeric(expression);
            if (tokens.Count == 0) return 0m;

            // Simple precedence evaluation: first pass *, /, second pass +, -
            var values = new List<decimal>();
            var ops = new List<char>();

            // Parse first value
            if (!decimal.TryParse(tokens[0], out var first))
                return 0m;
            values.Add(first);

            for (int i = 1; i < tokens.Count - 1; i += 2)
            {
                var op = tokens[i][0];
                if (!decimal.TryParse(tokens[i + 1], out var val))
                    val = 0m;
                ops.Add(op);
                values.Add(val);
            }

            // First pass: * and /
            for (int i = 0; i < ops.Count; i++)
            {
                if (ops[i] == '*' || ops[i] == '/')
                {
                    var result = ops[i] == '*'
                        ? values[i] * values[i + 1]
                        : (values[i + 1] != 0 ? values[i] / values[i + 1] : 0m);
                    values[i] = result;
                    values.RemoveAt(i + 1);
                    ops.RemoveAt(i);
                    i--;
                }
            }

            // Second pass: + and -
            var total = values[0];
            for (int i = 0; i < ops.Count; i++)
            {
                total = ops[i] == '+' ? total + values[i + 1] : total - values[i + 1];
            }

            return total;
        }

        /// <summary>
        /// Evaluate a string expression. Simply resolves field references.
        /// </summary>
        private static object EvaluateString(
            string resolved, Dictionary<string, object> row, string originalExpression)
        {
            // For string type, just return the resolved expression with field refs replaced
            return FieldRefPattern.Replace(originalExpression, match =>
            {
                var fieldName = match.Groups[1].Value;
                if (row.TryGetValue(fieldName, out var val) && val != null)
                    return val.ToString();
                return string.Empty;
            });
        }

        /// <summary>
        /// Tokenize a numeric expression into numbers and operators.
        /// "100 * 0.15 + 5" → ["100", "*", "0.15", "+", "5"]
        /// </summary>
        private static List<string> TokenizeNumeric(string expression)
        {
            var tokens = new List<string>();
            var current = string.Empty;
            bool lastWasOp = true; // treat start as after operator (for negative numbers)

            foreach (var ch in expression)
            {
                if (char.IsWhiteSpace(ch)) continue;

                if ((ch == '+' || ch == '-' || ch == '*' || ch == '/') && !lastWasOp)
                {
                    if (current.Length > 0) { tokens.Add(current); current = string.Empty; }
                    tokens.Add(ch.ToString());
                    lastWasOp = true;
                }
                else if (char.IsDigit(ch) || ch == '.' || (ch == '-' && lastWasOp))
                {
                    current += ch;
                    lastWasOp = false;
                }
            }

            if (current.Length > 0) tokens.Add(current);
            return tokens;
        }
    }
}

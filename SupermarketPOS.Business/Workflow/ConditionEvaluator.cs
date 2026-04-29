using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4, Step 2: Safe condition evaluator for workflow rules.
    /// Parses simple expressions like: Total > 10000 AND IsPaid == false
    /// Supports: ==, !=, >, &lt;, >=, &lt;=, AND, OR, field references.
    /// No dynamic compilation — pure string parsing with fail-safe design.
    /// </summary>
    public sealed class ConditionEvaluator
    {
        private static readonly Regex TokenPattern = new Regex(
            @"(?<string>'[^']*')|(?<number>\d+\.?\d*)|(?<bool>true|false)|(?<null>null)|(?<op>==|!=|>=|<=|>|<)|(?<logic>AND|OR)|(?<paren>[()])|(?<field>[A-Za-z_]\w*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool Evaluate(string expression, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            try
            {
                var tokens = Tokenize(expression);
                int pos = 0;
                return ParseOrExpression(tokens, ref pos, data);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            foreach (Match match in TokenPattern.Matches(expression))
            {
                if (match.Groups["string"].Success)
                    tokens.Add(new Token(TokenType.Value, match.Value.Trim('\'')));
                else if (match.Groups["number"].Success)
                    tokens.Add(new Token(TokenType.Value, match.Value));
                else if (match.Groups["bool"].Success)
                    tokens.Add(new Token(TokenType.Value, match.Value.ToLowerInvariant()));
                else if (match.Groups["null"].Success)
                    tokens.Add(new Token(TokenType.Value, null));
                else if (match.Groups["op"].Success)
                    tokens.Add(new Token(TokenType.Operator, match.Value));
                else if (match.Groups["logic"].Success)
                    tokens.Add(new Token(TokenType.Logic, match.Value.ToUpperInvariant()));
                else if (match.Groups["paren"].Success)
                    tokens.Add(new Token(TokenType.Paren, match.Value));
                else if (match.Groups["field"].Success)
                    tokens.Add(new Token(TokenType.Field, match.Value));
            }
            return tokens;
        }

        private bool ParseOrExpression(List<Token> tokens, ref int pos, Dictionary<string, object> data)
        {
            bool result = ParseAndExpression(tokens, ref pos, data);
            while (pos < tokens.Count && tokens[pos].Type == TokenType.Logic && tokens[pos].Value == "OR")
            {
                pos++;
                result = ParseAndExpression(tokens, ref pos, data) || result;
            }
            return result;
        }

        private bool ParseAndExpression(List<Token> tokens, ref int pos, Dictionary<string, object> data)
        {
            bool result = ParseComparison(tokens, ref pos, data);
            while (pos < tokens.Count && tokens[pos].Type == TokenType.Logic && tokens[pos].Value == "AND")
            {
                pos++;
                result = ParseComparison(tokens, ref pos, data) && result;
            }
            return result;
        }

        private bool ParseComparison(List<Token> tokens, ref int pos, Dictionary<string, object> data)
        {
            if (pos < tokens.Count && tokens[pos].Type == TokenType.Paren && tokens[pos].Value == "(")
            {
                pos++;
                bool result = ParseOrExpression(tokens, ref pos, data);
                if (pos < tokens.Count && tokens[pos].Type == TokenType.Paren && tokens[pos].Value == ")")
                    pos++;
                return result;
            }

            object left = ResolveValue(tokens, ref pos, data);

            if (pos >= tokens.Count || tokens[pos].Type != TokenType.Operator)
                return ConvertToBool(left);

            string op = tokens[pos].Value;
            pos++;

            object right = ResolveValue(tokens, ref pos, data);

            return Compare(left, op, right);
        }

        private object ResolveValue(List<Token> tokens, ref int pos, Dictionary<string, object> data)
        {
            if (pos >= tokens.Count)
                return null;

            var token = tokens[pos];
            pos++;

            if (token.Type == TokenType.Field)
            {
                if (data != null)
                {
                    var key = data.Keys.FirstOrDefault(k =>
                        k.Equals(token.Value, StringComparison.OrdinalIgnoreCase));
                    if (key != null)
                        return data[key];
                }
                return null;
            }

            if (token.Type == TokenType.Value)
                return token.Value;

            return null;
        }

        private bool Compare(object left, string op, object right)
        {
            if (left == null && right == null)
                return op == "==" || op == ">=" || op == "<=";
            if (left == null || right == null)
                return op == "!=";

            string leftStr = Convert.ToString(left, CultureInfo.InvariantCulture);
            string rightStr = Convert.ToString(right, CultureInfo.InvariantCulture);

            if (decimal.TryParse(leftStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal leftNum) &&
                decimal.TryParse(rightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rightNum))
            {
                switch (op)
                {
                    case "==": return leftNum == rightNum;
                    case "!=": return leftNum != rightNum;
                    case ">": return leftNum > rightNum;
                    case "<": return leftNum < rightNum;
                    case ">=": return leftNum >= rightNum;
                    case "<=": return leftNum <= rightNum;
                }
            }

            if (bool.TryParse(leftStr, out bool leftBool) && bool.TryParse(rightStr, out bool rightBool))
            {
                switch (op)
                {
                    case "==": return leftBool == rightBool;
                    case "!=": return leftBool != rightBool;
                }
            }

            int cmp = string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
            switch (op)
            {
                case "==": return cmp == 0;
                case "!=": return cmp != 0;
                case ">": return cmp > 0;
                case "<": return cmp < 0;
                case ">=": return cmp >= 0;
                case "<=": return cmp <= 0;
                default: return false;
            }
        }

        private bool ConvertToBool(object value)
        {
            if (value == null) return false;
            string s = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (bool.TryParse(s, out bool b)) return b;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d)) return d != 0;
            return !string.IsNullOrEmpty(s);
        }

        private enum TokenType { Field, Value, Operator, Logic, Paren }

        private struct Token
        {
            public TokenType Type;
            public string Value;
            public Token(TokenType type, string value) { Type = type; Value = value; }
        }
    }
}

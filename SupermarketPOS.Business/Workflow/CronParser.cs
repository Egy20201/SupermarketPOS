using System;
using System.Globalization;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4.5: Lightweight cron expression parser.
    /// Supports standard 5-field format: minute hour day month dayOfWeek.
    /// Wildcard (*) and fixed values supported. No ranges/steps for simplicity.
    /// </summary>
    public static class CronParser
    {
        public static DateTime? GetNextRun(string cronExpression, DateTime from)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return null;

            try
            {
                var parts = cronExpression.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    return null;

                string minuteExpr = parts[0];
                string hourExpr = parts[1];
                string dayExpr = parts[2];
                string monthExpr = parts[3];
                string dowExpr = parts[4];

                var candidate = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0);
                candidate = candidate.AddMinutes(1);

                for (int i = 0; i < 525960; i++)
                {
                    if (Matches(candidate.Month, monthExpr) &&
                        Matches(candidate.Day, dayExpr) &&
                        Matches((int)candidate.DayOfWeek, dowExpr) &&
                        Matches(candidate.Hour, hourExpr) &&
                        Matches(candidate.Minute, minuteExpr))
                    {
                        return candidate;
                    }

                    candidate = candidate.AddMinutes(1);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsDue(string cronExpression, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return false;

            try
            {
                var parts = cronExpression.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    return false;

                return Matches(now.Minute, parts[0]) &&
                       Matches(now.Hour, parts[1]) &&
                       Matches(now.Day, parts[2]) &&
                       Matches(now.Month, parts[3]) &&
                       Matches((int)now.DayOfWeek, parts[4]);
            }
            catch
            {
                return false;
            }
        }

        private static bool Matches(int value, string expr)
        {
            if (expr == "*") return true;

            if (int.TryParse(expr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int exact))
                return value == exact;

            if (expr.Contains(","))
            {
                foreach (var part in expr.Split(','))
                {
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v == value)
                        return true;
                }
                return false;
            }

            if (expr.Contains("/"))
            {
                var stepParts = expr.Split('/');
                if (stepParts.Length == 2 &&
                    int.TryParse(stepParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int step) &&
                    step > 0)
                {
                    return value % step == 0;
                }
            }

            return false;
        }
    }
}

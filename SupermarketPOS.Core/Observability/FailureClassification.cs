using System;

namespace SupermarketPOS.Core.Observability
{
    /// <summary>
    /// Phase 5, Step 10: Standard failure classifications.
    /// </summary>
    public static class FailureClassification
    {
        public const string ValidationError = "ValidationError";
        public const string BusinessRuleError = "BusinessRuleError";
        public const string SystemError = "SystemError";
        public const string TimeoutError = "TimeoutError";
        public const string CircuitBreakerOpen = "CircuitBreakerOpen";

        public static string Classify(Exception ex)
        {
            if (ex == null) return null;

            var typeName = ex.GetType().Name;

            if (typeName == "ValidationException")
                return ValidationError;

            if (typeName == "InvalidOperationException" || typeName == "ArgumentException")
                return BusinessRuleError;

            if (ex is OperationCanceledException || ex is TimeoutException)
                return TimeoutError;

            return SystemError;
        }
    }
}

using SupermarketPOS.Core.Observability;
using System;
using System.Diagnostics;

namespace SupermarketPOS.Business.Observability
{
    /// <summary>
    /// Phase 5, Step 9: Structured logging with CorrelationId, Entity, Operation, Duration.
    /// Wraps the existing Logger with structured context.
    /// </summary>
    public static class StructuredLogger
    {
        public static void Info(string entityName, string operation, string message)
        {
            var cid = CorrelationContext.CorrelationId ?? "-";
            Logger.Info($"[{cid}] [{entityName}] [{operation}] {message}");
        }

        public static void Debug(string entityName, string operation, string message)
        {
            var cid = CorrelationContext.CorrelationId ?? "-";
            Logger.Debug($"[{cid}] [{entityName}] [{operation}] {message}");
        }

        public static void Warning(string entityName, string operation, string message)
        {
            var cid = CorrelationContext.CorrelationId ?? "-";
            Logger.Warning($"[{cid}] [{entityName}] [{operation}] {message}");
        }

        public static void Error(string entityName, string operation, string message, Exception ex = null)
        {
            var cid = CorrelationContext.CorrelationId ?? "-";
            string errorDetail = ex != null ? $" | {ex.GetType().Name}: {ex.Message}" : "";
            Logger.Error($"[{cid}] [{entityName}] [{operation}] {message}{errorDetail}");
        }

        public static void OperationComplete(string entityName, string operation, long durationMs, bool success, string detail = null)
        {
            var cid = CorrelationContext.CorrelationId ?? "-";
            string status = success ? "OK" : "FAIL";
            string extra = !string.IsNullOrEmpty(detail) ? $" | {detail}" : "";
            Logger.Info($"[{cid}] [{entityName}] [{operation}] {status} {durationMs}ms{extra}");
        }
    }

    /// <summary>
    /// Simple logger facade. Replace with NLog/Serilog/etc in production.
    /// </summary>
    internal static class Logger
    {
        public static void Info(string message) => Trace.TraceInformation(message);
        public static void Debug(string message) => Trace.WriteLine(message, "DEBUG");
        public static void Warning(string message) => Trace.TraceWarning(message);
        public static void Error(string message) => Trace.TraceError(message);
    }
}

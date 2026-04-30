namespace SupermarketPOS.Business
{
    /// <summary>
    /// Lightweight in-process error counter. The class is non-static so it can
    /// be injected as a DI dependency, but the methods stay static so existing
    /// call sites (<c>MetricsService.RecordError()</c>) keep compiling unchanged.
    /// </summary>
    public class MetricsService
    {
        private static int _errorCount = 0;

        public static void RecordError()
        {
            _errorCount++;
        }

        public static int GetErrorCount() => _errorCount;
    }
}
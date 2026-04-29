namespace SupermarketPOS.Business
{
    public static class MetricsService
    {
        private static int _errorCount = 0;

        public static void RecordError()
        {
            _errorCount++;
        }

        public static int GetErrorCount() => _errorCount;
    }
}
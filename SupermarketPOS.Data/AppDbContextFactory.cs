namespace SupermarketPOS.Data
{
    public static class AppDbContextFactory
    {
        public static AppDbContext Create()
        {
            return new AppDbContext();
        }
    }
}

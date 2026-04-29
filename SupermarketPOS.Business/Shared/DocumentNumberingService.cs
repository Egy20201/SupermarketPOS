using SupermarketPOS.Data;
using System;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class DocumentNumberingService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public DocumentNumberingService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public string GenerateNumber(string prefix, string sequenceName)
        {
            using (var db = _dbFactory())
            {
                var seq = db.Database.SqlQuery<long>(
                    $"SELECT NEXT VALUE FOR {sequenceName}").First();
                return $"{prefix}{seq:D6}";
            }
        }
    }
}
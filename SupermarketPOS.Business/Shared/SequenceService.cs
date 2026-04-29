using SupermarketPOS.Data;
using System.Linq;

namespace SupermarketPOS.Business
{
    public static class SequenceService
    {
        public static int GetNextInvoiceNumber()
        {
            using (var db = new AppDbContext())
            {
                var last = db.SaleInvoices.OrderByDescending(i => i.Id).FirstOrDefault();
                return (last?.Id ?? 0) + 1;
            }
        }
    }
}
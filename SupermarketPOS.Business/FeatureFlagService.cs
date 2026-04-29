using SupermarketPOS.Data;
using System;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class FeatureFlagService
    {
        public bool IsEnabled(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return true;

            try
            {
                using (var db = new AppDbContext())
                {
                    var flag = db.FeatureFlags.FirstOrDefault(f => f.Code == code);
                    if (flag == null)
                        return true;

                    return flag.IsEnabled;
                }
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}

using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class ConfigurationService
    {
        private const string BusinessTypeSettingKey = "business.type";
        private static readonly object RefreshSync = new object();
        private static DateTime _lastRefreshAtUtc = DateTime.MinValue;
        private static string _lastBusinessType;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

        private static readonly Dictionary<string, Dictionary<string, bool>> BusinessProfiles =
            new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "supermarket",
                    new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "sales", true },
                        { "inventory", true },
                        { "purchases", true },
                        { "accounting", true },
                        { "supermarket.expiry_tracking", true },
                        { "pharmacy.expiry_tracking", false }
                    }
                },
                {
                    "pharmacy",
                    new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "sales", true },
                        { "inventory", true },
                        { "purchases", true },
                        { "accounting", true },
                        { "supermarket.expiry_tracking", false },
                        { "pharmacy.expiry_tracking", true }
                    }
                }
            };

        public void AutoApplyProfiles()
        {
            RefreshConfiguration();
        }

        public void RefreshConfiguration()
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                if (utcNow - _lastRefreshAtUtc < RefreshInterval)
                    return;

                lock (RefreshSync)
                {
                    utcNow = DateTime.UtcNow;
                    if (utcNow - _lastRefreshAtUtc < RefreshInterval)
                        return;

                    RefreshInternal();
                    _lastRefreshAtUtc = utcNow;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Auto apply configuration profile failed");
            }
        }

        private static void RefreshInternal()
        {
            using (var db = new AppDbContext())
            {
                var businessType = db.PlatformSettings
                    .Where(s => s.Key == BusinessTypeSettingKey && s.IsActive)
                    .OrderByDescending(s => s.Id)
                    .Select(s => s.Value)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(businessType))
                    return;

                if (!string.IsNullOrWhiteSpace(_lastBusinessType) &&
                    string.Equals(_lastBusinessType, businessType, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Dictionary<string, bool> profile;
                if (!BusinessProfiles.TryGetValue(businessType, out profile) || profile == null || profile.Count == 0)
                    return;

                var codes = profile.Keys.ToList();
                var flags = db.FeatureFlags
                    .Where(f => codes.Contains(f.Code))
                    .ToList();

                var hasChanges = false;

                foreach (var flag in flags)
                {
                    bool enabledValue;
                    if (!profile.TryGetValue(flag.Code, out enabledValue))
                        continue;

                    if (flag.IsEnabled == enabledValue)
                        continue;

                    flag.IsEnabled = enabledValue;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    db.SaveChanges();
                }
                _lastBusinessType = businessType;
            }
        }
    }
}

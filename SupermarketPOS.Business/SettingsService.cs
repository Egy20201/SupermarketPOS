using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class SettingsService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly ConcurrentDictionary<string, string> _cache;
        private readonly object _initLock = new object();
        private bool _isLoaded;

        public SettingsService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _cache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void LoadAll()
        {
            if (_isLoaded) return;
            lock (_initLock)
            {
                if (_isLoaded) return;
                using (var db = _dbFactory())
                {
                    var settings = db.Settings.Where(s => s.IsActive).ToList();
                    _cache.Clear();
                    foreach (var setting in settings)
                        _cache[setting.Key] = setting.Value ?? string.Empty;
                }
                _isLoaded = true;
                Logger.Info($"SettingsService loaded {_cache.Count} settings into cache");
            }
        }

        public string Get(string key, string defaultValue = null)
        {
            EnsureLoaded();
            if (_cache.TryGetValue(key, out var value)) return value;
            return defaultValue;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            var rawValue = Get(key, null);
            if (rawValue == null) return defaultValue;
            try
            {
                var targetType = typeof(T);
                if (targetType == typeof(int)) return (T)(object)int.Parse(rawValue, CultureInfo.InvariantCulture);
                if (targetType == typeof(decimal)) return (T)(object)decimal.Parse(rawValue, CultureInfo.InvariantCulture);
                if (targetType == typeof(bool)) return (T)(object)bool.Parse(rawValue);
                if (targetType == typeof(string)) return (T)(object)rawValue;
                if (targetType == typeof(DateTime)) return (T)(object)DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
                return (T)Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                Logger.Error($"Failed to convert setting '{key}' value '{rawValue}' to {typeof(T).Name}");
                return defaultValue;
            }
        }

        public bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);
        public int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);
        public decimal GetDecimal(string key, decimal defaultValue = 0m) => Get(key, defaultValue);

        public void Set(string key, string value, string groupName = null, string dataType = null, string description = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Setting key cannot be empty", nameof(key));
            EnsureLoaded();
            using (var db = _dbFactory())
            {
                var setting = db.Settings.FirstOrDefault(s => s.Key == key);
                if (setting == null)
                {
                    setting = new Setting
                    {
                        Key = key,
                        Value = value,
                        GroupName = groupName ?? "عام",
                        DataType = dataType ?? "string",
                        Description = description,
                        IsActive = true,
                        UpdatedAt = DateTime.Now
                    };
                    db.Settings.Add(setting);
                }
                else
                {
                    setting.Value = value; setting.UpdatedAt = DateTime.Now;
                    if (groupName != null) setting.GroupName = groupName;
                    if (dataType != null) setting.DataType = dataType;
                    if (description != null) setting.Description = description;
                }
                db.SaveChanges();
            }
            _cache[key] = value ?? string.Empty;
            Logger.Info($"Setting updated: {key} = {value}");
        }

        public Dictionary<string, List<SettingDto>> GetAllGrouped()
        {
            EnsureLoaded();
            using (var db = _dbFactory())
            {
                return db.Settings.Where(s => s.IsActive).OrderBy(s => s.GroupName).ThenBy(s => s.Key).ToList()
                    .GroupBy(s => s.GroupName ?? "عام")
                    .ToDictionary(g => g.Key, g => g.Select(s => new SettingDto
                    {
                        Id = s.Id,
                        Key = s.Key,
                        Value = s.Value,
                        DataType = s.DataType,
                        Description = s.Description,
                        UpdatedAt = s.UpdatedAt
                    }).ToList());
            }
        }

        public void Refresh() { _isLoaded = false; LoadAll(); }
        private void EnsureLoaded() { if (!_isLoaded) LoadAll(); }
    }

    public class SettingDto
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
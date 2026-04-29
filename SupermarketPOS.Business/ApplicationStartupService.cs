using SupermarketPOS.Business.Metadata;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class ApplicationStartupService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly ConfigurationService _configurationService;
        private readonly OutboxBackgroundProcessor _outboxProcessor;
        private readonly MetadataSeeder _metadataSeeder;
        private readonly MetadataRegistryService _metadataRegistry;

        public ApplicationStartupService(
            Func<AppDbContext> dbFactory,
            ConfigurationService configurationService,
            OutboxBackgroundProcessor outboxProcessor,
            MetadataSeeder metadataSeeder,
            MetadataRegistryService metadataRegistry)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _outboxProcessor = outboxProcessor ?? throw new ArgumentNullException(nameof(outboxProcessor));
            _metadataSeeder = metadataSeeder ?? throw new ArgumentNullException(nameof(metadataSeeder));
            _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        }

        public void EnsureSeedData()
        {
            AppDbContext.SeedData();
            _metadataSeeder.SeedIfEmpty();
            _metadataRegistry.LoadAll();
            MetadataValidator.Validate(_metadataRegistry);
            _outboxProcessor.Start();
        }

        public bool RequiresBusinessTypeSetup()
        {
            using (var db = _dbFactory())
            {
                return !db.PlatformSettings.Any(s => s.Key == "business.type" && s.IsActive);
            }
        }

        public void ApplyBusinessType(string businessType)
        {
            if (string.IsNullOrWhiteSpace(businessType))
                throw new ArgumentException("Business type is required.", nameof(businessType));

            using (var db = _dbFactory())
            {
                var existing = db.PlatformSettings.FirstOrDefault(s => s.Key == "business.type");
                if (existing == null)
                {
                    db.PlatformSettings.Add(new PlatformSetting
                    {
                        Key = "business.type",
                        Value = businessType,
                        Scope = "Global",
                        IsActive = true
                    });
                }
                else
                {
                    existing.Value = businessType;
                    existing.IsActive = true;
                }

                db.SaveChanges();
            }

            _configurationService.RefreshConfiguration();
        }
    }
}

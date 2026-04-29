using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Database-driven metadata service. The system describes itself through this service.
    /// Loads entity/field definitions from the in-memory registry (fast O(1) lookups)
    /// and fetches layouts/actions from DB on demand.
    /// </summary>
    public sealed class MetadataService : IMetadataService
    {
        private readonly MetadataRegistryService _registry;
        private readonly Func<AppDbContext> _dbFactory;

        public MetadataService(MetadataRegistryService registry, Func<AppDbContext> dbFactory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <inheritdoc />
        public EntityDefinition GetEntity(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _registry.GetEntity(name);
        }

        /// <inheritdoc />
        public IReadOnlyList<FieldDefinition> GetFields(int entityId)
        {
            var entity = _registry.GetEntity(entityId);
            if (entity == null) return Array.Empty<FieldDefinition>();
            return (IReadOnlyList<FieldDefinition>)entity.Fields;
        }

        /// <inheritdoc />
        public FormLayoutDefinition GetFormLayout(int entityId)
        {
            using (var db = _dbFactory())
            {
                return db.FormLayoutDefinitions
                    .AsNoTracking()
                    .FirstOrDefault(f => f.EntityId == entityId);
            }
        }

        /// <inheritdoc />
        public GridLayoutDefinition GetGridLayout(int entityId)
        {
            using (var db = _dbFactory())
            {
                return db.GridLayoutDefinitions
                    .AsNoTracking()
                    .FirstOrDefault(g => g.EntityId == entityId);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<ActionDefinition> GetActions(int entityId)
        {
            using (var db = _dbFactory())
            {
                return db.ActionDefinitions
                    .AsNoTracking()
                    .Where(a => a.EntityId == entityId)
                    .OrderBy(a => a.Name)
                    .ToList()
                    .AsReadOnly();
            }
        }
    }
}

using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// The brain of the metadata system. Loads all entity/field/relation definitions from DB
    /// once at startup, caches them in memory, and provides O(1) lookups by name.
    /// 
    /// Thread-safe: uses Interlocked.Exchange for atomic snapshot replacement.
    /// No DB calls after LoadAll(). No reflection. No allocations per lookup.
    /// </summary>
    public sealed class MetadataRegistryService
    {
        private readonly Func<AppDbContext> _dbFactory;

        // Immutable snapshot replaced atomically via Interlocked.Exchange
        private volatile MetadataSnapshot _snapshot = MetadataSnapshot.Empty;

        public MetadataRegistryService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// Loads ALL metadata from DB into memory. Called once at startup.
        /// Subsequent calls reload (useful for admin refresh scenarios).
        /// </summary>
        public void LoadAll()
        {
            using (var db = _dbFactory())
            {
                // Single round-trip: eager-load the full graph
                var modules = db.ModuleDefinitions
                    .Where(m => m.IsActive)
                    .ToList();

                var entities = db.EntityDefinitions
                    .Include(e => e.Module)
                    .Include(e => e.Fields)
                    .Where(e => e.IsActive)
                    .ToList();

                var relations = db.RelationDefinitions
                    .Include(r => r.SourceEntity)
                    .Include(r => r.TargetEntity)
                    .ToList();

                // Wire relations into entities (EF6 may not auto-populate inverse collections)
                foreach (var rel in relations)
                {
                    var source = entities.FirstOrDefault(e => e.Id == rel.SourceEntityId);
                    var target = entities.FirstOrDefault(e => e.Id == rel.TargetEntityId);

                    if (source != null && !source.OutgoingRelations.Any(r => r.Id == rel.Id))
                        source.OutgoingRelations.Add(rel);

                    if (target != null && !target.IncomingRelations.Any(r => r.Id == rel.Id))
                        target.IncomingRelations.Add(rel);
                }

                // Sort fields by OrderIndex for deterministic ordering
                foreach (var entity in entities)
                {
                    var sorted = entity.Fields.OrderBy(f => f.OrderIndex ?? int.MaxValue).ToList();
                    entity.Fields = sorted;
                }

                // Build O(1) lookup dictionaries
                var byName = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
                var byId = new Dictionary<int, EntityDefinition>();

                foreach (var entity in entities)
                {
                    byName[entity.Name] = entity;
                    byId[entity.Id] = entity;
                }

                var modulesByName = new Dictionary<string, ModuleDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var mod in modules)
                    modulesByName[mod.Name] = mod;

                // Atomic snapshot replacement - no locking needed for readers
                var snapshot = new MetadataSnapshot(byName, byId, modulesByName, entities, modules, relations);
                Interlocked.Exchange(ref _snapshot, snapshot);

                Logger.Info($"Metadata loaded: {entities.Count} entities, " +
                           $"{entities.Sum(e => e.Fields.Count)} fields, " +
                           $"{relations.Count} relations, " +
                           $"{modules.Count} modules");
            }
        }

        /// <summary>O(1) entity lookup by name. Returns null if not found.</summary>
        public EntityDefinition GetEntity(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _snapshot.EntitiesByName.TryGetValue(name, out var entity);
            return entity;
        }

        /// <summary>O(1) entity lookup by database ID. Returns null if not found.</summary>
        public EntityDefinition GetEntity(int id)
        {
            _snapshot.EntitiesById.TryGetValue(id, out var entity);
            return entity;
        }

        /// <summary>Returns all active entities. No allocation - returns cached list.</summary>
        public IReadOnlyList<EntityDefinition> GetAllEntities()
        {
            return _snapshot.AllEntities;
        }

        /// <summary>O(1) field list for an entity. Returns empty list if entity not found.</summary>
        public IReadOnlyList<FieldDefinition> GetFields(string entityName)
        {
            var entity = GetEntity(entityName);
            if (entity == null) return Array.Empty<FieldDefinition>();
            return (IReadOnlyList<FieldDefinition>)entity.Fields;
        }

        /// <summary>O(1) module lookup by name. Returns null if not found.</summary>
        public ModuleDefinition GetModule(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _snapshot.ModulesByName.TryGetValue(name, out var module);
            return module;
        }

        /// <summary>Returns all active modules.</summary>
        public IReadOnlyList<ModuleDefinition> GetAllModules()
        {
            return _snapshot.AllModules;
        }

        /// <summary>Returns all relations.</summary>
        public IReadOnlyList<RelationDefinition> GetAllRelations()
        {
            return _snapshot.AllRelations;
        }

        /// <summary>Returns outgoing relations for an entity (e.g., Sale -> Customer).</summary>
        public IReadOnlyList<RelationDefinition> GetOutgoingRelations(string entityName)
        {
            var entity = GetEntity(entityName);
            if (entity == null) return Array.Empty<RelationDefinition>();
            return (IReadOnlyList<RelationDefinition>)entity.OutgoingRelations;
        }

        /// <summary>True if metadata has been loaded at least once.</summary>
        public bool IsLoaded => _snapshot != MetadataSnapshot.Empty;

        /// <summary>
        /// Immutable snapshot of all metadata. Replaced atomically on reload.
        /// Readers never see a partially-built state.
        /// </summary>
        private sealed class MetadataSnapshot
        {
            public static readonly MetadataSnapshot Empty = new MetadataSnapshot(
                new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<int, EntityDefinition>(),
                new Dictionary<string, ModuleDefinition>(StringComparer.OrdinalIgnoreCase),
                new List<EntityDefinition>(),
                new List<ModuleDefinition>(),
                new List<RelationDefinition>());

            public readonly Dictionary<string, EntityDefinition> EntitiesByName;
            public readonly Dictionary<int, EntityDefinition> EntitiesById;
            public readonly Dictionary<string, ModuleDefinition> ModulesByName;
            public readonly IReadOnlyList<EntityDefinition> AllEntities;
            public readonly IReadOnlyList<ModuleDefinition> AllModules;
            public readonly IReadOnlyList<RelationDefinition> AllRelations;

            public MetadataSnapshot(
                Dictionary<string, EntityDefinition> byName,
                Dictionary<int, EntityDefinition> byId,
                Dictionary<string, ModuleDefinition> modulesByName,
                List<EntityDefinition> allEntities,
                List<ModuleDefinition> allModules,
                List<RelationDefinition> allRelations)
            {
                EntitiesByName = byName;
                EntitiesById = byId;
                ModulesByName = modulesByName;
                AllEntities = allEntities.AsReadOnly();
                AllModules = allModules.AsReadOnly();
                AllRelations = allRelations.AsReadOnly();
            }
        }
    }
}

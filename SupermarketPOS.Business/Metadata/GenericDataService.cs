using Newtonsoft.Json;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Phase 2 + 2.5: Hardened Generic Data Engine.
    /// Executes CRUD against any metadata-registered entity via parameterized SQL.
    /// 
    /// Production features (Phase 2):
    ///   - Retry policy: deadlock (#1205) and timeout (#-2), exponential backoff, max 3 retries
    ///   - Transaction support: shared connection via AsyncLocal, full rollback on failure
    ///   - Query safety: default limit 100, hard cap 1000 — no unbounded scans
    ///   - Metadata cache: ConcurrentDictionary avoids repeated registry lookups
    ///   - Safe logging: entity names only, no raw SQL written to logs
    ///   - Command timeout: 30 seconds on every command
    ///   - Performance guard: warns on filterless queries
    ///   - Metadata validation: integrated via MetadataValidationService
    ///   - Default values: applied from metadata before insert
    ///   - Lookup resolution: JOINs target tables for display values
    ///   - Action engine: executes Create/Save/Delete/Approve dynamically
    /// 
    /// Enterprise hardening (Phase 2.5):
    ///   - Field-level permissions: read/write enforcement per role
    ///   - Audit trail: automatic logging of Create/Update/Delete with old/new values
    ///   - Computed fields: post-query evaluation from metadata expressions
    ///   - Bulk operations: BulkCreateAsync/BulkUpdateAsync in single transactions
    ///   - Query plan cache: cached SQL per entity + filter shape
    ///   - Action extensibility: IActionHandler pattern with DI-registered handlers
    ///   - Domain event integration: OutboxEvent for EntityCreated/Updated/Deleted
    ///   - Performance guards: entity MaxRows, CancellationToken support
    /// </summary>
    public sealed class GenericDataService : IGenericDataService
    {
        // ================================================================
        //  Configuration
        // ================================================================

        private const int DefaultQueryLimit = 100;
        private const int MaxQueryLimit = 1000;
        private const int CommandTimeoutSeconds = 30;
        private const int MaxRetries = 3;
        private const int RetryBaseDelayMs = 200;
        private const int BulkBatchSize = 100;

        // SQL Server transient error numbers
        private const int DeadlockErrorNumber = 1205;
        private const int TimeoutErrorNumber = -2;

        // ================================================================
        //  Dependencies
        // ================================================================

        private readonly Func<AppDbContext> _dbFactory;
        private readonly MetadataRegistryService _registry;
        private readonly IMetadataValidationService _validator;
        private readonly IMetadataService _metadataService;
        private readonly AuditService _auditService;
        private readonly FieldPermissionService _fieldPermissions;
        private readonly IActionHandler[] _actionHandlers;

        // Local metadata cache — avoids repeated registry lookups per entity
        private readonly ConcurrentDictionary<string, ResolvedMetadata> _cache =
            new ConcurrentDictionary<string, ResolvedMetadata>(StringComparer.OrdinalIgnoreCase);

        // Transaction context flowing through async calls
        private static readonly AsyncLocal<TransactionContext> _txContext =
            new AsyncLocal<TransactionContext>();

        // Engine session context (userId, roleId) flowing through async calls
        private static readonly AsyncLocal<EngineSession> _sessionContext =
            new AsyncLocal<EngineSession>();

        /// <summary>
        /// Gets or sets the engine session for the current async flow.
        /// Set this before calling engine methods to enable audit trail and field permissions.
        /// </summary>
        public static EngineSession CurrentSession
        {
            get => _sessionContext.Value;
            set => _sessionContext.Value = value;
        }

        public GenericDataService(
            Func<AppDbContext> dbFactory,
            MetadataRegistryService registry,
            IMetadataValidationService validator,
            IMetadataService metadataService,
            AuditService auditService,
            FieldPermissionService fieldPermissions,
            IEnumerable<IActionHandler> actionHandlers)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _fieldPermissions = fieldPermissions ?? throw new ArgumentNullException(nameof(fieldPermissions));
            _actionHandlers = actionHandlers?.ToArray() ?? Array.Empty<IActionHandler>();
        }

        // ================================================================
        //  QUERY (legacy)
        // ================================================================

        public async Task<List<Dictionary<string, object>>> QueryAsync(
            string entityName, QueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var meta = ResolveCached(entityName);
            ValidateQueryRequest(meta.AllFields, request);
            EnforceQueryLimits(request, meta.Entity);

            // Performance guard: warn on filterless queries
            if (request.Filters == null || request.Filters.Count == 0)
                Logger.Warning($"[GenericData] QUERY {meta.Entity.Name}: " +
                              $"no filters (limit={request.Limit}) — potential full scan");

            Logger.Debug($"[GenericData] QUERY {meta.Entity.Name} " +
                        $"(filters={request.Filters?.Count ?? 0}, limit={request.Limit}, " +
                        $"offset={request.Offset ?? 0})");

            var results = await WithConnectionAsync(meta.Entity.Name, "QUERY",
                async (conn, tx) =>
                {
                    var (sql, parameters) = SqlQueryBuilder.BuildSelect(
                        meta.Entity, meta.AllFields, request);

                    using (var cmd = conn.CreateCommand())
                    {
                        if (tx != null) cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = CommandTimeoutSeconds;
                        foreach (var p in parameters) cmd.Parameters.Add(p);

                        var rows = new List<Dictionary<string, object>>();
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                var row = new Dictionary<string, object>(
                                    StringComparer.OrdinalIgnoreCase);
                                for (int i = 0; i < reader.FieldCount; i++)
                                    row[reader.GetName(i)] = reader.IsDBNull(i)
                                        ? null : reader.GetValue(i);
                                rows.Add(row);
                            }
                        }
                        return rows;
                    }
                }).ConfigureAwait(false);

            Logger.Debug($"[GenericData] QUERY {meta.Entity.Name}: {results.Count} row(s)");
            return results;
        }

        // ================================================================
        //  GET (with QueryOptions + lookup resolution + computed fields)
        // ================================================================

        public async Task<List<Dictionary<string, object>>> GetAsync(
            string entityName, QueryOptions options)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));

            options = options ?? new QueryOptions();
            var meta = ResolveCached(entityName);
            var request = options.ToQueryRequest();
            ValidateQueryRequest(meta.AllFields, request);
            EnforceQueryLimits(request, meta.Entity);

            Logger.Debug($"[GenericData] GET {meta.Entity.Name} " +
                        $"(filters={request.Filters?.Count ?? 0}, page={options.Page}, " +
                        $"pageSize={options.PageSize}, lookups={options.ResolveLookups})");

            bool hasLookups = options.ResolveLookups &&
                meta.AllFields.Any(f =>
                    f.DataType != null &&
                    f.DataType.Equals("lookup", StringComparison.OrdinalIgnoreCase) &&
                    f.LookupEntityId.HasValue);

            var results = await WithConnectionAsync(meta.Entity.Name, "GET",
                async (conn, tx) =>
                {
                    string sql;
                    List<SqlParameter> parameters;

                    // Step 5: Try query plan cache
                    var cachedSql = QueryPlanCache.TryGetSelect(entityName, request, hasLookups);

                    if (cachedSql != null)
                    {
                        // Rebuild parameters only (SQL structure is cached)
                        if (hasLookups)
                        {
                            (_, parameters) = SqlQueryBuilder.BuildSelectWithLookups(
                                meta.Entity, meta.AllFields, request,
                                lookupEntityId => _registry.GetEntity(lookupEntityId));
                        }
                        else
                        {
                            (_, parameters) = SqlQueryBuilder.BuildSelect(
                                meta.Entity, meta.AllFields, request);
                        }
                        sql = cachedSql;
                    }
                    else
                    {
                        if (hasLookups)
                        {
                            (sql, parameters) = SqlQueryBuilder.BuildSelectWithLookups(
                                meta.Entity, meta.AllFields, request,
                                lookupEntityId => _registry.GetEntity(lookupEntityId));
                        }
                        else
                        {
                            (sql, parameters) = SqlQueryBuilder.BuildSelect(
                                meta.Entity, meta.AllFields, request);
                        }

                        // Cache the generated SQL for future reuse
                        QueryPlanCache.StoreSelect(entityName, request, hasLookups, sql);
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        if (tx != null) cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = CommandTimeoutSeconds;
                        foreach (var p in parameters) cmd.Parameters.Add(p);

                        var rows = new List<Dictionary<string, object>>();
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                var row = new Dictionary<string, object>(
                                    StringComparer.OrdinalIgnoreCase);
                                for (int i = 0; i < reader.FieldCount; i++)
                                    row[reader.GetName(i)] = reader.IsDBNull(i)
                                        ? null : reader.GetValue(i);
                                rows.Add(row);
                            }
                        }
                        return rows;
                    }
                }).ConfigureAwait(false);

            // Step 3: Evaluate computed fields post-query
            ComputedFieldEvaluator.EvaluateAll(meta.AllFields, results);

            // Step 1: Filter readable fields based on role permissions
            var session = CurrentSession;
            _fieldPermissions.FilterReadableRows(meta.Entity.Id, session?.RoleId, results);

            Logger.Debug($"[GenericData] GET {meta.Entity.Name}: {results.Count} row(s)");
            return results;
        }

        // ================================================================
        //  INSERT (legacy — no validation, no defaults)
        // ================================================================

        public async Task<int> InsertAsync(string entityName, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (data == null || data.Count == 0)
                throw new ArgumentException("Data is required for insert.", nameof(data));

            var meta = ResolveCached(entityName);
            ValidateInsertData(meta.AllFields, data);

            // Step 3: Strip computed fields from insert data
            StripComputedFields(meta.AllFields, data);

            Logger.Debug($"[GenericData] INSERT {meta.Entity.Name} ({data.Count} field(s))");

            int newId = await ExecuteInsertAsync(meta, data).ConfigureAwait(false);

            Logger.Debug($"[GenericData] INSERT {meta.Entity.Name}: Id={newId}");
            return newId;
        }

        // ================================================================
        //  CREATE (validated + defaults applied + audit + events)
        // ================================================================

        public async Task<int> CreateAsync(string entityName, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));

            data = data ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var meta = ResolveCached(entityName);
            var session = CurrentSession;

            // Step 3: Strip computed fields
            StripComputedFields(meta.AllFields, data);

            // Step 1: Filter writable fields based on role permissions
            data = _fieldPermissions.FilterWritableData(meta.Entity.Id, session?.RoleId, data);

            // Step 1: Apply default values from metadata
            DefaultValueResolver.ApplyDefaults(meta.AllFields, data);

            // Step 2: Validate via metadata rules (Required, MaxLength, DataType)
            var errors = _validator.Validate(entityName, data, isUpdate: false);
            if (errors.Count > 0)
                throw new ValidationException(entityName, errors);

            Logger.Debug($"[GenericData] CREATE {meta.Entity.Name} ({data.Count} field(s))");

            int newId = await ExecuteInsertAsync(meta, data).ConfigureAwait(false);

            // Step 2: Audit trail — log the create
            LogAudit("Create", entityName, newId, null, data);

            // Step 7: Publish domain event
            PublishDomainEvent("MetadataEntityCreated", entityName, newId, data);

            Logger.Debug($"[GenericData] CREATE {meta.Entity.Name}: Id={newId}");
            return newId;
        }

        // ================================================================
        //  UPDATE (with validation + audit + events)
        // ================================================================

        public async Task<int> UpdateAsync(string entityName, int id, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (id <= 0)
                throw new ArgumentException("Valid Id is required for update.", nameof(id));
            if (data == null || data.Count == 0)
                throw new ArgumentException("Data is required for update.", nameof(data));

            var meta = ResolveCached(entityName);
            var session = CurrentSession;

            // Step 3: Strip computed fields
            StripComputedFields(meta.AllFields, data);

            // Step 1: Filter writable fields based on role permissions
            data = _fieldPermissions.FilterWritableData(meta.Entity.Id, session?.RoleId, data);

            if (data.Count == 0 || (data.Count == 1 && data.ContainsKey("Id")))
                throw new ArgumentException("No writable fields provided for update.");

            // Validate via metadata rules (update mode: only provided fields checked)
            var errors = _validator.Validate(entityName, data, isUpdate: true);
            if (errors.Count > 0)
                throw new ValidationException(entityName, errors);

            ValidateUpdateData(meta.AllFields, data);

            // Step 2: Fetch old values for audit trail (only changed fields)
            Dictionary<string, object> oldValues = null;
            try
            {
                oldValues = await FetchRowAsync(meta, id, data.Keys.ToList()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[GenericData] Could not fetch old values for audit: {ex.Message}");
            }

            Logger.Debug($"[GenericData] UPDATE {meta.Entity.Name} Id={id} ({data.Count} field(s))");

            int rowsAffected = await WithConnectionAsync(meta.Entity.Name, "UPDATE",
                async (conn, tx) =>
                {
                    var (sql, parameters) = SqlQueryBuilder.BuildUpdate(
                        meta.Entity, meta.EditableFields, id, data);

                    using (var cmd = conn.CreateCommand())
                    {
                        if (tx != null) cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = CommandTimeoutSeconds;
                        foreach (var p in parameters) cmd.Parameters.Add(p);

                        return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);

            if (rowsAffected == 0)
                throw new InvalidOperationException(
                    $"'{meta.Entity.Name}' with Id={id} not found.");

            // Step 2: Audit trail — log the update with old/new values
            LogAudit("Update", entityName, id, oldValues, data);

            // Step 7: Publish domain event
            PublishDomainEvent("MetadataEntityUpdated", entityName, id, data);

            Logger.Debug($"[GenericData] UPDATE {meta.Entity.Name} Id={id}: {rowsAffected} row(s)");
            return rowsAffected;
        }

        // ================================================================
        //  DELETE (with audit + events)
        // ================================================================

        public async Task<int> DeleteAsync(string entityName, int id)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (id <= 0)
                throw new ArgumentException("Valid Id is required for delete.", nameof(id));

            var meta = ResolveCached(entityName);

            // Step 2: Fetch old values for audit trail before deletion
            Dictionary<string, object> oldValues = null;
            try
            {
                oldValues = await FetchRowAsync(meta, id, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[GenericData] Could not fetch old values for audit: {ex.Message}");
            }

            Logger.Debug($"[GenericData] DELETE {meta.Entity.Name} Id={id}");

            int rowsAffected = await WithConnectionAsync(meta.Entity.Name, "DELETE",
                async (conn, tx) =>
                {
                    var (sql, parameters) = SqlQueryBuilder.BuildDelete(meta.Entity, id);

                    using (var cmd = conn.CreateCommand())
                    {
                        if (tx != null) cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = CommandTimeoutSeconds;
                        foreach (var p in parameters) cmd.Parameters.Add(p);

                        return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);

            if (rowsAffected == 0)
                throw new InvalidOperationException(
                    $"'{meta.Entity.Name}' with Id={id} not found.");

            // Step 2: Audit trail — log the delete with old values
            LogAudit("Delete", entityName, id, oldValues, null);

            // Step 7: Publish domain event
            PublishDomainEvent("MetadataEntityDeleted", entityName, id, oldValues);

            Logger.Debug($"[GenericData] DELETE {meta.Entity.Name} Id={id}: done");
            return rowsAffected;
        }

        // ================================================================
        //  ACTION ENGINE (with extensible IActionHandler)
        // ================================================================

        /// <summary>
        /// Execute a metadata-defined action by name.
        /// First checks DI-registered IActionHandler implementations,
        /// then falls back to built-in Create/Save/Delete/Approve dispatch.
        /// All operations run inside a transaction.
        /// </summary>
        public async Task<object> ExecuteActionAsync(
            string entityName, string actionName, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (string.IsNullOrWhiteSpace(actionName))
                throw new ArgumentException("Action name is required.", nameof(actionName));

            var entity = _metadataService.GetEntity(entityName);
            if (entity == null)
                throw new ArgumentException($"Entity '{entityName}' not found in metadata.");

            var actions = _metadataService.GetActions(entity.Id);
            var action = actions.FirstOrDefault(a =>
                a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));

            if (action == null)
                throw new ArgumentException(
                    $"Action '{actionName}' not found for entity '{entityName}'.");

            Logger.Info($"[GenericData] ACTION {entityName}.{actionName} (Type={action.Type})");

            // Step 6: Check extensible action handlers first
            var handler = _actionHandlers.FirstOrDefault(h =>
                h.CanHandle(entityName, action.Type));

            if (handler != null)
            {
                Logger.Debug($"[GenericData] ACTION {entityName}.{actionName}: " +
                            $"using handler {handler.GetType().Name}");

                return await ExecuteInTransactionAsync(async svc =>
                {
                    return await handler.HandleAsync(
                        entityName, action, data ?? new Dictionary<string, object>(),
                        svc, CancellationToken.None).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            // Built-in dispatch based on action type — all inside a transaction
            return await ExecuteInTransactionAsync(async svc =>
            {
                switch (action.Type?.ToLowerInvariant())
                {
                    case "create":
                        return (object)await svc.CreateAsync(entityName, data ?? new Dictionary<string, object>())
                            .ConfigureAwait(false);

                    case "save":
                        data = data ?? new Dictionary<string, object>();
                        if (!data.TryGetValue("Id", out var idObj) || idObj == null)
                            throw new ArgumentException("Save action requires 'Id' in data.");
                        int saveId = Convert.ToInt32(idObj);
                        var updateData = new Dictionary<string, object>(data, StringComparer.OrdinalIgnoreCase);
                        updateData.Remove("Id");
                        return (object)await svc.UpdateAsync(entityName, saveId, updateData)
                            .ConfigureAwait(false);

                    case "delete":
                        data = data ?? new Dictionary<string, object>();
                        if (!data.TryGetValue("Id", out var delIdObj) || delIdObj == null)
                            throw new ArgumentException("Delete action requires 'Id' in data.");
                        int delId = Convert.ToInt32(delIdObj);
                        return (object)await svc.DeleteAsync(entityName, delId)
                            .ConfigureAwait(false);

                    case "approve":
                        // Approve = update a status field. Default behavior: set Status = "Approved"
                        data = data ?? new Dictionary<string, object>();
                        if (!data.TryGetValue("Id", out var appIdObj) || appIdObj == null)
                            throw new ArgumentException("Approve action requires 'Id' in data.");
                        int appId = Convert.ToInt32(appIdObj);
                        var approveData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Status"] = "Approved"
                        };
                        // Merge any extra fields from caller
                        if (data != null)
                        {
                            foreach (var kvp in data)
                            {
                                if (!kvp.Key.Equals("Id", StringComparison.OrdinalIgnoreCase))
                                    approveData[kvp.Key] = kvp.Value;
                            }
                        }
                        return (object)await svc.UpdateAsync(entityName, appId, approveData)
                            .ConfigureAwait(false);

                    default:
                        throw new InvalidOperationException(
                            $"Unknown action type '{action.Type}' for action '{actionName}'.");
                }
            }).ConfigureAwait(false);
        }

        // ================================================================
        //  BULK CREATE (Step 4)
        // ================================================================

        public async Task<List<int>> BulkCreateAsync(
            string entityName,
            List<Dictionary<string, object>> rows,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (rows == null || rows.Count == 0)
                throw new ArgumentException("At least one row is required.", nameof(rows));

            var meta = ResolveCached(entityName);
            var session = CurrentSession;
            var ids = new List<int>(rows.Count);

            Logger.Info($"[GenericData] BULK CREATE {meta.Entity.Name}: {rows.Count} row(s)");

            // Validate + apply defaults for all rows upfront
            var preparedRows = new List<Dictionary<string, object>>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var data = rows[i] ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                StripComputedFields(meta.AllFields, data);
                data = _fieldPermissions.FilterWritableData(meta.Entity.Id, session?.RoleId, data);
                DefaultValueResolver.ApplyDefaults(meta.AllFields, data);

                var errors = _validator.Validate(entityName, data, isUpdate: false);
                if (errors.Count > 0)
                    throw new ValidationException(entityName,
                        errors.Select(e => $"[Row {i}] {e}").ToList());

                preparedRows.Add(data);
            }

            // Execute in batches inside a transaction
            await ExecuteInTransactionAsync(async svc =>
            {
                for (int i = 0; i < preparedRows.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int newId = await svc.InsertAsync(entityName, preparedRows[i]).ConfigureAwait(false);
                    ids.Add(newId);
                }
                return true;
            }).ConfigureAwait(false);

            // Audit + events for all created rows
            for (int i = 0; i < ids.Count; i++)
            {
                LogAudit("Create", entityName, ids[i], null, preparedRows[i]);
                PublishDomainEvent("MetadataEntityCreated", entityName, ids[i], preparedRows[i]);
            }

            Logger.Info($"[GenericData] BULK CREATE {meta.Entity.Name}: {ids.Count} row(s) created");
            return ids;
        }

        // ================================================================
        //  BULK UPDATE (Step 4)
        // ================================================================

        public async Task<int> BulkUpdateAsync(
            string entityName,
            List<BulkUpdateItem> updates,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));
            if (updates == null || updates.Count == 0)
                throw new ArgumentException("At least one update is required.", nameof(updates));

            var meta = ResolveCached(entityName);
            var session = CurrentSession;

            Logger.Info($"[GenericData] BULK UPDATE {meta.Entity.Name}: {updates.Count} row(s)");

            // Validate all rows upfront
            for (int i = 0; i < updates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = updates[i];
                if (item.Id <= 0)
                    throw new ArgumentException($"[Row {i}] Valid Id is required for update.");
                if (item.Data == null || item.Data.Count == 0)
                    throw new ArgumentException($"[Row {i}] Data is required for update.");

                StripComputedFields(meta.AllFields, item.Data);
                item.Data = _fieldPermissions.FilterWritableData(meta.Entity.Id, session?.RoleId, item.Data);

                var errors = _validator.Validate(entityName, item.Data, isUpdate: true);
                if (errors.Count > 0)
                    throw new ValidationException(entityName,
                        errors.Select(e => $"[Row {i}, Id={item.Id}] {e}").ToList());
            }

            // Execute in a single transaction
            int totalAffected = 0;
            await ExecuteInTransactionAsync(async svc =>
            {
                for (int i = 0; i < updates.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int affected = await svc.UpdateAsync(entityName, updates[i].Id, updates[i].Data)
                        .ConfigureAwait(false);
                    totalAffected += affected;
                }
                return true;
            }).ConfigureAwait(false);

            Logger.Info($"[GenericData] BULK UPDATE {meta.Entity.Name}: {totalAffected} row(s) affected");
            return totalAffected;
        }

        // ================================================================
        //  TRANSACTION
        // ================================================================

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<IGenericDataService, Task<T>> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (_txContext.Value != null)
                throw new InvalidOperationException(
                    "[GenericData] Nested transactions are not supported.");

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (var db = _dbFactory())
                    {
                        var conn = db.Database.Connection;
                        if (conn.State != ConnectionState.Open)
                            await conn.OpenAsync().ConfigureAwait(false);

                        using (var tx = conn.BeginTransaction())
                        {
                            _txContext.Value = new TransactionContext(conn, tx);
                            try
                            {
                                var result = await action(this).ConfigureAwait(false);
                                tx.Commit();
                                Logger.Debug("[GenericData] Transaction committed");
                                return result;
                            }
                            catch
                            {
                                SafeRollback(tx);
                                throw;
                            }
                            finally
                            {
                                _txContext.Value = null;
                            }
                        }
                    }
                }
                catch (SqlException ex) when (IsTransient(ex) && attempt < MaxRetries)
                {
                    var delay = RetryBaseDelayMs * (1 << attempt);
                    Logger.Warning($"[GenericData] Transaction: transient error (#{ex.Number}), " +
                                  $"retry {attempt + 1}/{MaxRetries} in {delay}ms");
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                catch (SqlException ex)
                {
                    throw new InvalidOperationException(
                        $"[GenericData] Transaction failed: {ex.Message}", ex);
                }
            }
        }

        // ================================================================
        //  Shared INSERT implementation
        // ================================================================

        private async Task<int> ExecuteInsertAsync(ResolvedMetadata meta, Dictionary<string, object> data)
        {
            return await WithConnectionAsync(meta.Entity.Name, "INSERT",
                async (conn, tx) =>
                {
                    var (sql, parameters) = SqlQueryBuilder.BuildInsert(
                        meta.Entity, meta.AllFields, data);

                    using (var cmd = conn.CreateCommand())
                    {
                        if (tx != null) cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = CommandTimeoutSeconds;
                        foreach (var p in parameters) cmd.Parameters.Add(p);

                        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                        return Convert.ToInt32(result);
                    }
                }).ConfigureAwait(false);
        }

        // ================================================================
        //  Fetch Row (for audit old-values capture)
        // ================================================================

        /// <summary>
        /// Fetch a single row's field values for audit trail.
        /// If fieldNames is null, fetches all fields.
        /// </summary>
        private async Task<Dictionary<string, object>> FetchRowAsync(
            ResolvedMetadata meta, int id, List<string> fieldNames)
        {
            var request = new QueryRequest
            {
                Limit = 1,
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { FieldName = "Id", Operator = "eq", Value = id }
                }
            };

            if (fieldNames != null && fieldNames.Count > 0)
            {
                request.SelectFields = fieldNames
                    .Where(f => !f.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (!request.SelectFields.Contains("Id"))
                    request.SelectFields.Insert(0, "Id");
            }

            var rows = await WithConnectionAsync(meta.Entity.Name, "FETCH",
                async (conn, tx) =>
                {
                    var (sql, parameters) = SqlQueryBuilder.BuildSelect(
                        meta.Entity, meta.AllFields, request);

                    using (var cmd = conn.CreateCommand())
                    {
                        if (tx != null) cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = CommandTimeoutSeconds;
                        foreach (var p in parameters) cmd.Parameters.Add(p);

                        var result = new List<Dictionary<string, object>>();
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            if (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                for (int i = 0; i < reader.FieldCount; i++)
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                result.Add(row);
                            }
                        }
                        return result;
                    }
                }).ConfigureAwait(false);

            return rows.Count > 0 ? rows[0] : null;
        }

        // ================================================================
        //  Audit Trail (Step 2)
        // ================================================================

        /// <summary>
        /// Fire-and-forget audit log entry via the existing AuditService.
        /// Non-blocking: audit failure does not affect the main operation.
        /// </summary>
        private void LogAudit(string action, string entityName, int? entityId,
            object oldValues, object newValues)
        {
            try
            {
                var session = CurrentSession;
                int userId = session?.UserId ?? 0;

                _auditService.LogChange(action, entityName, entityId, userId, oldValues, newValues);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[GenericData] Audit log failed for {action} on {entityName}: {ex.Message}");
            }
        }

        // ================================================================
        //  Domain Events (Step 7)
        // ================================================================

        /// <summary>
        /// Publish a domain event to the OutboxEvents table.
        /// Uses a separate connection — event persistence is best-effort.
        /// </summary>
        private void PublishDomainEvent(string eventType, string entityName, int? entityId,
            object data)
        {
            try
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    EntityName = entityName,
                    EntityId = entityId,
                    Data = data,
                    Timestamp = DateTime.UtcNow,
                    UserId = CurrentSession?.UserId
                });

                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "INSERT INTO [OutboxEvents] ([EventType], [Payload], [CreatedAt], [Status], [RetryCount]) " +
                                          "VALUES (@evtType, @payload, @createdAt, @status, 0)";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@evtType", eventType));
                        cmd.Parameters.Add(new SqlParameter("@payload", payload));
                        cmd.Parameters.Add(new SqlParameter("@createdAt", DateTime.UtcNow));
                        cmd.Parameters.Add(new SqlParameter("@status", "Pending"));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[GenericData] Domain event publish failed ({eventType} for {entityName}): {ex.Message}");
            }
        }

        // ================================================================
        //  Computed Fields Helper (Step 3)
        // ================================================================

        /// <summary>
        /// Remove computed fields from a data dictionary before INSERT/UPDATE.
        /// Computed fields are calculated post-query, not stored.
        /// </summary>
        private static void StripComputedFields(
            IReadOnlyList<FieldDefinition> fields, Dictionary<string, object> data)
        {
            foreach (var field in fields)
            {
                if (field.IsComputed)
                {
                    // Remove by matching key case-insensitively
                    var key = data.Keys.FirstOrDefault(k =>
                        k.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                    if (key != null)
                        data.Remove(key);
                }
            }
        }

        // ================================================================
        //  Connection + Retry Engine
        // ================================================================

        /// <summary>
        /// Executes a DB operation with automatic connection management and retry.
        /// Inside a transaction: uses shared connection, no per-operation retry.
        /// Standalone: new connection per attempt, retries on transient errors.
        /// SQL is rebuilt on each retry attempt (SqlParameter can't be reused).
        /// </summary>
        private async Task<T> WithConnectionAsync<T>(
            string entityName, string operation,
            Func<DbConnection, DbTransaction, Task<T>> action)
        {
            var txCtx = _txContext.Value;

            if (txCtx != null)
            {
                // Inside transaction — shared connection, no per-operation retry.
                // Transient errors bubble up to ExecuteInTransactionAsync for batch retry.
                return await action(txCtx.Connection, txCtx.Transaction)
                    .ConfigureAwait(false);
            }

            // Standalone — new connection per attempt + exponential backoff retry
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (var db = _dbFactory())
                    {
                        var conn = db.Database.Connection;
                        if (conn.State != ConnectionState.Open)
                            await conn.OpenAsync().ConfigureAwait(false);

                        return await action(conn, null).ConfigureAwait(false);
                    }
                }
                catch (SqlException ex) when (IsTransient(ex) && attempt < MaxRetries)
                {
                    var delay = RetryBaseDelayMs * (1 << attempt); // 200, 400, 800ms
                    Logger.Warning($"[GenericData] {operation} {entityName}: " +
                                  $"transient error (#{ex.Number}), " +
                                  $"retry {attempt + 1}/{MaxRetries} in {delay}ms");
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                catch (SqlException ex)
                {
                    throw new InvalidOperationException(
                        $"[GenericData] {operation} failed for '{entityName}': {ex.Message}", ex);
                }
            }
        }

        /// <summary>Deadlock (#1205) or command timeout (#-2).</summary>
        private static bool IsTransient(SqlException ex)
        {
            return ex.Number == DeadlockErrorNumber
                || ex.Number == TimeoutErrorNumber;
        }

        private static void SafeRollback(DbTransaction tx)
        {
            try { tx.Rollback(); }
            catch (Exception rbEx)
            {
                Logger.Warning($"[GenericData] Rollback failed: {rbEx.Message}");
            }
        }

        // ================================================================
        //  Metadata Cache
        // ================================================================

        /// <summary>
        /// Resolve entity + fields from cache. First call per entity name hits the registry;
        /// subsequent calls return the cached result in O(1).
        /// </summary>
        private ResolvedMetadata ResolveCached(string entityName)
        {
            return _cache.GetOrAdd(entityName, name =>
            {
                if (!_registry.IsLoaded)
                    throw new InvalidOperationException(
                        "Metadata registry not loaded. Call LoadAll() at startup.");

                var entity = _registry.GetEntity(name);
                if (entity == null)
                    throw new ArgumentException(
                        $"Entity '{name}' not found in metadata registry.");

                var fields = (IReadOnlyList<FieldDefinition>)entity.Fields;
                if (fields == null || fields.Count == 0)
                    throw new InvalidOperationException(
                        $"Entity '{entity.Name}' has no fields defined in metadata.");

                return new ResolvedMetadata(entity, fields);
            });
        }

        // ================================================================
        //  Query Safety (with entity MaxRows — Step 8)
        // ================================================================

        /// <summary>
        /// Enforce query limits: default 100, hard cap 1000.
        /// Also respects entity-level MaxRows from metadata.
        /// Prevents unbounded full-table scans.
        /// </summary>
        private static void EnforceQueryLimits(QueryRequest request, EntityDefinition entity)
        {
            if (!request.Limit.HasValue || request.Limit.Value <= 0)
                request.Limit = DefaultQueryLimit;
            else if (request.Limit.Value > MaxQueryLimit)
                request.Limit = MaxQueryLimit;

            // Step 8: Entity-specific MaxRows guard
            if (entity.MaxRows.HasValue && entity.MaxRows.Value > 0)
            {
                if (request.Limit.Value > entity.MaxRows.Value)
                {
                    Logger.Debug($"[GenericData] Clamping limit from {request.Limit} to entity MaxRows={entity.MaxRows} for {entity.Name}");
                    request.Limit = entity.MaxRows.Value;
                }
            }
        }

        // ================================================================
        //  Validation (legacy — kept for InsertAsync/QueryAsync backward compat)
        // ================================================================

        /// <summary>Validate all referenced fields exist in metadata.</summary>
        private static void ValidateQueryRequest(
            IReadOnlyList<FieldDefinition> fields, QueryRequest request)
        {
            if (request.SelectFields != null)
                foreach (var name in request.SelectFields)
                    AssertFieldExists(fields, name, "Select");

            if (request.Filters != null)
                foreach (var f in request.Filters)
                    AssertFieldExists(fields, f.FieldName, "Filter");

            if (request.SortBy != null)
                foreach (var s in request.SortBy)
                    AssertFieldExists(fields, s.FieldName, "Sort");
        }

        /// <summary>Validate required fields present + all fields recognized.</summary>
        private static void ValidateInsertData(
            IReadOnlyList<FieldDefinition> fields, Dictionary<string, object> data)
        {
            // Required fields must have non-null values
            foreach (var fd in fields)
            {
                if (!fd.IsRequired) continue;

                bool hasValue = data.Any(d =>
                    d.Key.Equals(fd.Name, StringComparison.OrdinalIgnoreCase)
                    && d.Value != null);

                if (!hasValue)
                    throw new ArgumentException(
                        $"Required field '{fd.Name}' ({fd.DisplayName ?? fd.Name}) is missing or null.");
            }

            // All provided keys must exist in metadata (except Id which is auto-skipped)
            foreach (var key in data.Keys)
            {
                if (key.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                if (!fields.Any(f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException(
                        $"Field '{key}' does not exist in metadata for this entity.");
            }
        }

        /// <summary>Validate all update fields exist and are editable.</summary>
        private static void ValidateUpdateData(
            IReadOnlyList<FieldDefinition> allFields, Dictionary<string, object> data)
        {
            foreach (var key in data.Keys)
            {
                if (key.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;

                var field = allFields.FirstOrDefault(
                    f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

                if (field == null)
                    throw new ArgumentException(
                        $"Field '{key}' does not exist in metadata.");

                if (!field.IsEditable)
                    throw new ArgumentException(
                        $"Field '{key}' ({field.DisplayName ?? key}) is not editable.");
            }
        }

        /// <summary>Check that a field name is either "Id" or exists in metadata.</summary>
        private static void AssertFieldExists(
            IReadOnlyList<FieldDefinition> fields, string name, string context)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"{context} field name cannot be empty.");
            if (name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                return;
            if (!fields.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    $"{context} field '{name}' does not exist in metadata.");
        }

        // ================================================================
        //  Inner Types
        // ================================================================

        /// <summary>
        /// Cached metadata for a single entity: entity definition + field lists.
        /// Built once per entity name, stored in ConcurrentDictionary.
        /// </summary>
        private sealed class ResolvedMetadata
        {
            public readonly EntityDefinition Entity;
            public readonly IReadOnlyList<FieldDefinition> AllFields;
            public readonly IReadOnlyList<FieldDefinition> EditableFields;

            public ResolvedMetadata(
                EntityDefinition entity, IReadOnlyList<FieldDefinition> allFields)
            {
                Entity = entity;
                AllFields = allFields;
                EditableFields = allFields.Where(f => f.IsEditable && !f.IsComputed).ToList();
            }
        }

        /// <summary>
        /// Shared connection + transaction for ExecuteInTransactionAsync.
        /// Flows through async calls via AsyncLocal.
        /// </summary>
        private sealed class TransactionContext
        {
            public readonly DbConnection Connection;
            public readonly DbTransaction Transaction;

            public TransactionContext(DbConnection conn, DbTransaction tx)
            {
                Connection = conn;
                Transaction = tx;
            }
        }
    }

    // ================================================================
    //  Engine Session Context
    // ================================================================

    /// <summary>
    /// Session context for the Generic Data Engine.
    /// Set via GenericDataService.CurrentSession before calling engine methods
    /// to enable audit trail, field permissions, and domain events.
    /// </summary>
    public class EngineSession
    {
        public int? UserId { get; set; }
        public int? RoleId { get; set; }
        public string UserName { get; set; }
    }

    // ================================================================
    //  Validation Exception
    // ================================================================

    /// <summary>
    /// Thrown when metadata validation fails during Create or Update.
    /// Contains all validation errors in a structured list.
    /// </summary>
    public class ValidationException : Exception
    {
        public string EntityName { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationException(string entityName, List<string> errors)
            : base($"Validation failed for '{entityName}': {string.Join("; ", errors)}")
        {
            EntityName = entityName;
            Errors = errors.AsReadOnly();
        }
    }
}

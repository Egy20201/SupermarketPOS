using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Phase 2 + 2.5: Generic Data Engine contract.
    /// Performs CRUD on any metadata-registered entity using dynamic SQL.
    /// No DbSet. No typed entities. No reflection.
    /// </summary>
    public interface IGenericDataService
    {
        /// <summary>
        /// Query rows from any entity. Returns list of column->value dictionaries.
        /// </summary>
        Task<List<Dictionary<string, object>>> QueryAsync(string entityName, QueryRequest request);

        /// <summary>
        /// Insert a row. Returns the new auto-generated Id.
        /// </summary>
        Task<int> InsertAsync(string entityName, Dictionary<string, object> data);

        /// <summary>
        /// Update a row by Id. Returns number of rows affected (1 or 0).
        /// </summary>
        Task<int> UpdateAsync(string entityName, int id, Dictionary<string, object> data);

        /// <summary>
        /// Delete a row by Id. Returns number of rows affected (1 or 0).
        /// </summary>
        Task<int> DeleteAsync(string entityName, int id);

        /// <summary>
        /// Execute multiple operations in a single DB transaction.
        /// Retries the entire batch on transient errors (deadlock/timeout).
        /// Rolls back on any failure.
        /// </summary>
        Task<T> ExecuteInTransactionAsync<T>(Func<IGenericDataService, Task<T>> action);

        // ================================================================
        //  Phase 2 Extensions: Validated CRUD + Action Engine
        // ================================================================

        /// <summary>
        /// Create a new entity row. Applies default values from metadata,
        /// validates via MetadataValidationService, then inserts.
        /// Returns the new auto-generated Id.
        /// </summary>
        Task<int> CreateAsync(string entityName, Dictionary<string, object> data);

        /// <summary>
        /// Query rows with QueryOptions (filters, sort, paging, lookup resolution).
        /// Lookup fields are automatically JOINed to return display values.
        /// </summary>
        Task<List<Dictionary<string, object>>> GetAsync(string entityName, QueryOptions options);

        /// <summary>
        /// Execute a metadata-defined action by name.
        /// Dispatches to Create/Save/Delete based on ActionDefinition.Type.
        /// Returns the result (new Id for Create, affected rows for Save/Delete).
        /// </summary>
        Task<object> ExecuteActionAsync(string entityName, string actionName, Dictionary<string, object> data);

        // ================================================================
        //  Phase 2.5: Enterprise Hardening Extensions
        // ================================================================

        /// <summary>
        /// Bulk-insert multiple rows in a single transaction.
        /// Validates + applies defaults per row. Returns list of new Ids.
        /// </summary>
        Task<List<int>> BulkCreateAsync(
            string entityName,
            List<Dictionary<string, object>> rows,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Bulk-update multiple rows in a single transaction.
        /// Validates per row. Returns total rows affected.
        /// </summary>
        Task<int> BulkUpdateAsync(
            string entityName,
            List<BulkUpdateItem> updates,
            CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>
    /// Represents a single row update in a bulk operation.
    /// </summary>
    public class BulkUpdateItem
    {
        public int Id { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }
}

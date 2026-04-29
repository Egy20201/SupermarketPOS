using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Phase 2.5: Extensible action handler contract.
    /// Implement this to add custom action logic (e.g., Approve, Reject, Submit)
    /// without modifying GenericDataService.
    /// 
    /// Registered via DI. GenericDataService checks all handlers before falling
    /// back to built-in Create/Save/Delete dispatch.
    /// </summary>
    public interface IActionHandler
    {
        /// <summary>
        /// Returns true if this handler can process the given entity + action type combination.
        /// Called once per action execution to find the correct handler.
        /// </summary>
        bool CanHandle(string entityName, string actionType);

        /// <summary>
        /// Execute the action. Runs inside the engine's transaction context.
        /// Returns the result (new Id for creates, affected rows for updates, etc.).
        /// </summary>
        Task<object> HandleAsync(
            string entityName,
            ActionDefinition action,
            Dictionary<string, object> data,
            IGenericDataService engine,
            CancellationToken cancellationToken);
    }
}

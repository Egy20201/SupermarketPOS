using SupermarketPOS.Data;

namespace SupermarketPOS.Business
{
    public interface IInventoryMovementService
    {
        bool DecreaseStockAndRecordMovement(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage);
        bool IncreaseStockAndRecordMovement(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage);
        bool SetStockAndRecordMovement(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage);
        bool TransferStock(AppDbContext db, int productId, int fromWarehouseId, int toWarehouseId, int quantity, string reference, out string errorMessage);
    }
}

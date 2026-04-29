using SupermarketPOS.Core.Entities;
using System.Collections.Generic;

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Branch { get; set; }
    public string Location { get; set; }
    public string Manager { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public ICollection<ProductStock> ProductStocks { get; set; }
    public ICollection<StockMovement> StockMovements { get; set; }
}
using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business
{
    public class DailySalesReportDto
    {
        public DateTime Date { get; set; }
        public int InvoiceCount { get; set; }
        public int ItemCount { get; set; }
        public decimal GrossSales { get; set; }
        public decimal Discounts { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetSales { get; set; }
        public decimal CashSales { get; set; }
        public decimal CreditSales { get; set; }
        public decimal Profit { get; set; }
    }

    public class TopProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public string CategoryName { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal ProfitMargin { get; set; }
    }

    public class InventoryStatusDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public string CategoryName { get; set; }
        public string WarehouseName { get; set; }
        public int Quantity { get; set; }
        public decimal AverageCost { get; set; }
        public decimal TotalValue { get; set; }
        public decimal SellingPrice { get; set; }
        public int ReorderLevel { get; set; }
        public bool NeedsReorder { get; set; }
        public decimal PotentialRevenue { get; set; }
    }

    public class ProfitReportDto
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public int InvoiceCount { get; set; }
        public int ItemCount { get; set; }
        public decimal GrossSales { get; set; }
        public decimal SalesReturns { get; set; }
        public decimal NetSales { get; set; }
        public decimal CashSales { get; set; }
        public decimal CreditSales { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal Expenses { get; set; }
        public decimal TotalCosts { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
    }

    public class CategorySalesDto
    {
        public string CategoryName { get; set; }
        public int ItemCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
    }

    public class PaymentMethodSummaryDto
    {
        public string PaymentType { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class LowStockAlertDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
        public int ShortageQuantity { get; set; }
    }

    public class SupplierPurchaseSummaryDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalPurchases { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalOutstanding { get; set; }
    }

    // Phase 5: Branch DTOs
    public class BranchSalesDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal CashSales { get; set; }
        public decimal CreditSales { get; set; }
        public int CustomerCount { get; set; }
    }

    public class TopBranchDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal ProfitMargin { get; set; }
        public int InvoiceCount { get; set; }
        public decimal AverageInvoiceValue { get; set; }
    }

    public class HqDashboardDto
    {
        public DateTime ReportDate { get; set; }
        public int TotalBranches { get; set; }
        public int ActiveBranches { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalCosts { get; set; }
        public int TotalInvoices { get; set; }
        public int TotalCustomers { get; set; }
        public List<BranchSalesDto> BranchBreakdown { get; set; }
        public List<TopBranchDto> TopBranches { get; set; }
    }

    public class BranchComparisonDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public decimal CurrentSales { get; set; }
        public decimal PreviousSales { get; set; }
        public decimal GrowthPercent { get; set; }
        public decimal CurrentProfit { get; set; }
        public decimal PreviousProfit { get; set; }
        public decimal ProfitGrowthPercent { get; set; }
    }

    public class AttendanceReportDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeCode { get; set; }
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int LateDays { get; set; }
        public int LeaveDays { get; set; }
        public decimal TotalLateMinutes { get; set; }
        public decimal TotalOvertimeMinutes { get; set; }
    }

    public class PayrollReportDto
    {
        public string PayrollNumber { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int EmployeeCount { get; set; }
        public decimal TotalNetSalary { get; set; }
        public string Status { get; set; }
    }
}
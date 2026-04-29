using Microsoft.Extensions.DependencyInjection;
using SupermarketPOS.Business.Metadata;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddBusinessServices(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddSharedServices();
            services.AddSecurityServices();
            services.AddBusinessModules();

            return services;
        }

        private static IServiceCollection AddSharedServices(this IServiceCollection services)
        {
            services.AddSingleton<Func<AppDbContext>>(_ => AppDbContextFactory.Create);
            services.AddSingleton<TransactionExecutor>();

            services.AddSingleton<ApplicationStartupService>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<FeatureFlagService>();
            services.AddSingleton<SettingsService>(sp =>
            {
                var service = new SettingsService(sp.GetRequiredService<Func<AppDbContext>>());
                service.LoadAll();
                return service;
            });
            services.AddSingleton<DocumentNumberingService>();
            services.AddSingleton<DomainEventService>();
            services.AddSingleton<OutboxBackgroundProcessor>();
            services.AddSingleton<SystemHealthService>();
            services.AddSingleton<MetadataSeeder>();
            services.AddSingleton<MetadataRegistryService>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton<IMetadataValidationService, MetadataValidationService>();

            // Phase 2.5: Enterprise Hardening
            services.AddSingleton<FieldPermissionService>();

            // Phase 2 + 2.5: Generic Data Engine (with hardening dependencies)
            services.AddSingleton<IGenericDataService>(sp =>
            {
                var dbFactory = sp.GetRequiredService<Func<AppDbContext>>();
                var registry = sp.GetRequiredService<MetadataRegistryService>();
                var validator = sp.GetRequiredService<IMetadataValidationService>();
                var metadataService = sp.GetRequiredService<IMetadataService>();
                var auditService = sp.GetRequiredService<AuditService>();
                var fieldPermissions = sp.GetRequiredService<FieldPermissionService>();
                var actionHandlers = sp.GetServices<IActionHandler>() ?? Enumerable.Empty<IActionHandler>();
                return new GenericDataService(
                    dbFactory, registry, validator, metadataService,
                    auditService, fieldPermissions, actionHandlers);
            });
            services.AddSingleton<IDomainEventHandler<DeliveryPostedEvent>, DeliveryPostedHandler>();
            services.AddSingleton<IDomainEventHandler<GoodsReceiptPostedEvent>, GoodsReceiptPostedHandler>();
            services.AddSingleton<IDomainEventHandler<CreditNotePostedEvent>, CreditNotePostedHandler>();
            services.AddSingleton<IDomainEventHandler<DebitNotePostedEvent>, DebitNotePostedHandler>();

            return services;
        }

        private static IServiceCollection AddSecurityServices(this IServiceCollection services)
        {
            services.AddSingleton<AuthenticationService>();
            services.AddSingleton<AuthorizationService>();
            services.AddSingleton<AuditService>();

            return services;
        }

        private static IServiceCollection AddBusinessModules(this IServiceCollection services)
        {
            services.AddAccountingModule();
            services.AddCustomerModule();
            services.AddInventoryModule();
            services.AddPurchaseModule();
            services.AddSalesModule();
            services.AddReportsModule();
            services.AddHrModule();
            services.AddAdminModule();

            return services;
        }

        private static IServiceCollection AddInventoryModule(this IServiceCollection services)
        {
            services.AddSingleton<InventoryService>();
            services.AddSingleton<IInventoryMovementService>(sp => sp.GetRequiredService<InventoryService>());
            services.AddSingleton<ProductService>();
            services.AddSingleton<CategoryService>();
            services.AddSingleton<UnitService>();
            services.AddSingleton<ProductUnitService>();
            services.AddSingleton<BulkProductService>();
            services.AddSingleton<InventoryModuleService>();

            return services;
        }

        private static IServiceCollection AddSalesModule(this IServiceCollection services)
        {
            services.AddSingleton<SalesService>();
            services.AddSingleton<SalesWorkflowService>();
            services.AddSingleton<SalesReturnService>();

            return services;
        }

        private static IServiceCollection AddPurchaseModule(this IServiceCollection services)
        {
            services.AddSingleton<PurchaseService>();
            services.AddSingleton<PurchaseWorkflowService>();
            services.AddSingleton<PurchaseReturnService>();

            return services;
        }

        private static IServiceCollection AddCustomerModule(this IServiceCollection services)
        {
            services.AddSingleton<CustomerService>();
            services.AddSingleton<CrmService>();
            services.AddSingleton<PartyAccountService>();

            return services;
        }

        private static IServiceCollection AddAccountingModule(this IServiceCollection services)
        {
            services.AddSingleton<AccountingService>();
            services.AddSingleton<PaymentService>();

            return services;
        }

        private static IServiceCollection AddReportsModule(this IServiceCollection services)
        {
            services.AddSingleton<ReportService>();
            services.AddSingleton<ReportsEngine>();
            services.AddSingleton<OperationalReportService>();

            return services;
        }

        private static IServiceCollection AddHrModule(this IServiceCollection services)
        {
            services.AddSingleton<EmployeeService>();
            services.AddSingleton<AttendanceService>();
            services.AddSingleton<PayrollService>();
            services.AddSingleton<LeaveRequestService>();

            return services;
        }

        private static IServiceCollection AddAdminModule(this IServiceCollection services)
        {
            services.AddSingleton<AdminService>();

            return services;
        }
    }
}

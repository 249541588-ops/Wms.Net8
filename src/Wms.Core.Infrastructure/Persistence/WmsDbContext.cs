using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Entities.Warehouse;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.Inbound;
using Wms.Core.Domain.Entities.Outbound;
using Wms.Core.Domain.Entities.Transport;
using Wms.Core.Domain.Entities.Counting;
using Wms.Core.Domain.Entities.StockFlow;
using Wms.Core.Domain.Entities.Archive;
using Wms.Core.Domain.Entities.System;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Wms.Core.Infrastructure.Persistence;

/// <summary>
/// WMS 智能仓储系统 EF Core 数据库上下文
/// </summary>
public class WmsDbContext : DbContext
{
    /// <summary>
    /// 初始化 WmsDbContext
    /// </summary>
    public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
    {
    }

    // Identity / 权限
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<SystemLog> SystemLogs { get; set; }
    public DbSet<AuthSetting> AuthSettings { get; set; }
    public DbSet<Menus> Menus { get; set; }
    public DbSet<Role_Menu> Role_Menus { get; set; }
    public DbSet<Role_Menu_Funs> Role_Menu_Funs { get; set; }
    public DbSet<UserRoles> UserRoles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Sys_Language> Sys_Languages { get; set; }

    // 基础数据
    public DbSet<BasicDictionary> BasicDictionaries { get; set; }

    // 仓库基础
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Rack> Racks { get; set; }
    public DbSet<Laneway> Laneways { get; set; }
    public DbSet<LanewayPort> LanewayPorts { get; set; }
    public DbSet<LanewayUsage> LanewayUsages { get; set; }
    public DbSet<Cell> Cells { get; set; }
    public DbSet<Port> Ports { get; set; }

    // 物料
    public DbSet<Materials> Materials { get; set; }
    public DbSet<StockStatusInfo> StockStatusInfos { get; set; }
    public DbSet<Stock> Stocks { get; set; }

    // 容器/托盘
    public DbSet<Unitload> Unitloads { get; set; }
    public DbSet<UnitloadItem> UnitloadItems { get; set; }
    public DbSet<UnitloadItemDetail> UnitloadItemDetails { get; set; }
    public DbSet<UnitloadOp> UnitloadOps { get; set; }
    public DbSet<UnionUnitload> UnionUnitloads { get; set; }
    public DbSet<UnionUnitloadItem> UnionUnitloadItems { get; set; }
    public DbSet<BatteryCell> BatteryCells { get; set; }
    public DbSet<BatteryOp> BatteryOps { get; set; }
    public DbSet<BatteryCellSorting> BatteryCellSortings { get; set; }

    // 入库
    public DbSet<InboundOrder> InboundOrders { get; set; }
    public DbSet<InboundLine> InboundLines { get; set; }
    public DbSet<BizTypeInfo> BizTypeInfos { get; set; }

    // 出库
    public DbSet<OutboundOrder> OutboundOrders { get; set; }
    public DbSet<OutboundLine> OutboundLines { get; set; }
    public DbSet<OutboundLineAllocation> OutboundLineAllocations { get; set; }
    public DbSet<OutboundBatch> OutboundBatches { get; set; }
    public DbSet<Wave> Waves { get; set; }
    public DbSet<WaveLine> WaveLines { get; set; }

    // 任务/搬运
    public DbSet<TransTask> TransTasks { get; set; }
    public DbSet<UploadMesInfo> UploadMesInfos { get; set; }


    // 盘点
    public DbSet<CountingOrder> CountingOrders { get; set; }
    public DbSet<CountingLine> CountingLines { get; set; }
    public DbSet<CountingLineItem> CountingLineItems { get; set; }
    public DbSet<CountingLineItemDetail> CountingLineItemDetails { get; set; }

    // 库存流水
    public DbSet<Flow> Flows { get; set; }
    public DbSet<BatchCount> BatchCounts { get; set; }
    public DbSet<MonthlyReport> MonthlyReports { get; set; }
    public DbSet<MonthlyReportEntry> MonthlyReportEntries { get; set; }

    // 归档
    public DbSet<ArchivedTask> ArchivedTasks { get; set; }
    public DbSet<ArchivedUnitload> ArchivedUnitloads { get; set; }
    public DbSet<ArchivedUnitloadItem> ArchivedUnitloadItems { get; set; }
    public DbSet<ArchivedUnitloadItemDetail> ArchivedUnitloadItemDetails { get; set; }

    // 系统
    public DbSet<BackgroundJob> BackgroundJobs { get; set; }
    public DbSet<AppSeq> AppSeqs { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
    public DbSet<Ocv3ScanCodeBatchProcess> Ocv3ScanCodeBatchProcesses { get; set; }
    public DbSet<AllowedOpType> AllowedOpTypes { get; set; }
    public DbSet<RoleOpType> RoleOpTypes { get; set; }
    public DbSet<LocationAllocRuleStat> LocationAllocRuleStats { get; set; }
    public DbSet<LocationOp> LocationOps { get; set; }
    public DbSet<WasteBatchSetting> WasteBatchSettings { get; set; }

    // 流程引擎
    public DbSet<FlowTemplate> FlowTemplates { get; set; }
    public DbSet<FlowNode> FlowNodes { get; set; }
    public DbSet<FlowInstance> FlowInstances { get; set; }
    public DbSet<FlowNodeLog> FlowNodeLogs { get; set; }

    // 报表
    public DbSet<ReportConfig> ReportConfigs { get; set; }
    public DbSet<UserReportConfig> UserReportConfigs { get; set; }
    public DbSet<ReportExportTask> ReportExportTasks { get; set; }

    /// <summary>
    /// 配置模型
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 自动加载所有 IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly);
    }

    /// <summary>
    /// 重写 SaveChangesAsync 以自动填充审计字段
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (EntityEntry entry in entries)
        {
            var auditable = (IAuditable)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                auditable.CreatedTime = DateTime.Now;
                auditable.ModifiedTime = DateTime.Now;
            }
            else if (entry.State == EntityState.Modified)
            {
                auditable.ModifiedTime = DateTime.Now;
                // 防止修改 CreatedTime
                entry.Property("CreatedTime").IsModified = false;
                entry.Property("CreatedBy").IsModified = false;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
